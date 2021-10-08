using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Linq;
using System.Numerics;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI.Core;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace App1
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private readonly MainViewModel viewModel = MainViewModel.Instance;

        private readonly Flyout pathBoxFlyout;

        private bool FilterChanged;

        public MainPage()
        {
            InitializeComponent();

            // If we're using the custom titlebar
            if (viewModel.UseTitlebar)
            {
                if (App.CurrentWindow.m_appWindow != null && AppWindowTitleBar.IsCustomizationSupported())
                {
                    AppWindowTitleBar titleBar = App.CurrentWindow.m_appWindow.TitleBar;
                    titleBar.ButtonBackgroundColor = Colors.Transparent;
                    titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                    App.CurrentWindow.m_appWindow.Title = viewModel.Title;
                    titleBar.ExtendsContentIntoTitleBar = true;
                }
                else
                {
                    App.CurrentWindow.Title = viewModel.Title;
                    App.CurrentWindow.ExtendsContentIntoTitleBar = true;
                }
                App.CurrentWindow.SetTitleBar(AppTitleBar);
            }
            else
            {
                if (App.CurrentWindow.m_appWindow != null && AppWindowTitleBar.IsCustomizationSupported())
                {
                    App.CurrentWindow.m_appWindow.Title = viewModel.Title;
                }
                else
                {
                    App.CurrentWindow.Title = viewModel.Title;
                }
                AppTitleBar.Visibility = Visibility.Collapsed;
            }

            foreach (string fileType in viewModel.FilteredFileTypes)
            {
                foreach (MenuFlyoutItemBase item in FilterMenu.Items)
                {
                    if (item.Tag != null && item.Tag.ToString() == fileType)
                    {
                        (item as ToggleMenuFlyoutItem).IsChecked = true;
                    }
                }
            }

            if (viewModel.CurrentFolder != null)
            {
                FolderChanged();
            }

            NotificationBox.Translation += new Vector3(0, 0, 32);
            ImageBorder.Translation += new Vector3(0, 0, 8);
            CategoryGrid.Translation += new Vector3(0, 0, 8);
            SharedThemeShadow.Receivers.Add(LayoutRoot);
            pathBoxFlyout = Resources["PathBoxFlyout"] as Flyout;
        }

        private async void PickFolderButton_Click(object sender, RoutedEventArgs e)
        {
            FolderPicker folderPicker = new()
            {
                SuggestedStartLocation = PickerLocationId.Desktop
            };

            foreach (string fileType in viewModel.FilteredFileTypes)
            {
                folderPicker.FileTypeFilter.Add(fileType);
            }

#if !UNIVERSAL
            // When running on win32, FileOpenPicker needs to know the top-level hwnd via IInitializeWithWindow::Initialize.
            IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.CurrentWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);
#endif

            viewModel.CurrentFolder = await folderPicker.PickSingleFolderAsync();
        }

        public void FolderChanged()
        {
            RefreshButton.IsEnabled = true;
            ConvertButton.IsEnabled = true;
            //ConvertMenu.IsEnabled = true;
            NewCategoryButton.IsEnabled = true;
            ImagePreview.Source = null;
            fileList.SelectedIndex = 0;
            if (App.CurrentWindow.m_appWindow != null && AppWindowTitleBar.IsCustomizationSupported())
            {
                App.CurrentWindow.m_appWindow.Title = viewModel.Title;
            }
            else
            {
                App.CurrentWindow.Title = viewModel.Title;
            }
        }

        private async void FileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (fileList.SelectedIndex == -1) { return; }

            viewModel.CurrentFile = viewModel.CurrentFiles.ElementAt(fileList.SelectedIndex);
            string fileType = viewModel.CurrentFile.FileType.ToUpperInvariant().Replace(".", "");
            if (fileType == "JPG") { fileType = "JPEG"; }
            else if (fileType == "WDP") { fileType = "JXR"; }

            foreach (MenuFlyoutItemBase item in ConvertMenu.Items)
            {
                if (item.Tag != null)
                {
                    item.Visibility = item.Tag.ToString() == fileType ? Visibility.Collapsed : Visibility.Visible;
                }
            }

            StaticInfoList.ItemsSource = await viewModel.GetFileInfo();
            //InfoList.ItemsSource = await viewModel.GetEditableFileInfo();
        }

        private void MoveButton_Click(object sender, RoutedEventArgs e)
        {
            //if (fileList.SelectedIndex == -1 || FolderList.SelectedIndex == -1) { return; }

            //StorageFolder folder = folders[FolderList.SelectedIndex];

            //await MoveFile(currentFile, folder);
        }

        private void RefreshFilesButton_Click(object sender, RoutedEventArgs e)
        {
            viewModel.GetFiles(viewModel.CurrentFolder);
        }

        private void ConvertOption_Click(object sender, RoutedEventArgs e)
        {
            string option = ((MenuFlyoutItem)sender).Tag.ToString();
            viewModel.ConvertFile(viewModel.CurrentFile, option);
        }

        private void NewCategoryAddButton_Click(object sender, RoutedEventArgs e)
        {
            NewCategoryButton.Flyout.Hide();
            viewModel.NewFolder(NewCatName.Text, viewModel.CurrentFolder);
        }

        private void NewCatName_TextChanged(object sender, TextChangedEventArgs e)
        {
            NewCatAddButton.IsEnabled = NewCatName.Text.Length != 0;
        }

        private async void MoveTo(StorageFolder folder)
        {
            int idx = viewModel.CurrentFiles.IndexOf(viewModel.CurrentFile);
            try
            {
                await viewModel.MoveFile(viewModel.CurrentFile, folder);
            }
            catch
            {
                if (viewModel.ShowMoveDialog ?? true)
                {
                    CustomContentDialog errorDialog = new();
                    errorDialog.XamlRoot = ImagePreview.XamlRoot;

                    ContentDialogResult result = await errorDialog.ShowAsync();

                    switch (result)
                    {
                        case ContentDialogResult.Primary:
                            await viewModel.MoveFile(viewModel.CurrentFile, folder, NameCollisionOption.GenerateUniqueName);
                            ShowNotificationBox();
                            UndoButton.IsEnabled = true;
                            break;
                        case ContentDialogResult.None:
                            ShowNotificationBox(true);
                            break;
                        case ContentDialogResult.Secondary:
                            await viewModel.MoveFile(viewModel.CurrentFile, folder, NameCollisionOption.ReplaceExisting);
                            UndoButton.IsEnabled = true;
                            break;
                        default:
                            break;
                    }
                }
                else
                {
                    if (viewModel.MoveConflictOption == 0)
                    {
                        return;
                    }
                    else if (viewModel.MoveConflictOption == 1)
                    {
                        StorageFile currentFile = viewModel.CurrentFile;
                        await viewModel.MoveFile(currentFile, folder, NameCollisionOption.GenerateUniqueName);
                        ShowNotificationBox();
                    }
                    else if (viewModel.MoveConflictOption == 2)
                    {
                        await viewModel.MoveFile(viewModel.CurrentFile, folder, NameCollisionOption.ReplaceExisting);
                        ShowNotificationBox(true);
                    }
                }
            }
            finally
            {
                fileList.SelectedIndex = idx;
                _ = fileList.Focus(FocusState.Programmatic);
                UndoButton.IsEnabled = true;
            }

        }

        private void CategoryGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            MoveTo(e.ClickedItem as StorageFolder);
        }

        private void RefreshCategoriesButton_Click(object sender, RoutedEventArgs e)
        {
            viewModel.GetFolders(viewModel.CurrentFolder);
        }

        private void ToggleMenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            ToggleMenuFlyoutItem item = sender as ToggleMenuFlyoutItem;
            string optionTag = item.Tag.ToString();

            _ = item.IsChecked ? viewModel.FilteredFileTypes.Add(optionTag) : viewModel.FilteredFileTypes.Remove(optionTag);
            FilterChanged = true;
        }

        private void FilterMenu_Closed(object sender, object e)
        {
            if (FilterChanged)
            {
                viewModel.SaveFilterList();
                viewModel.GetFiles(viewModel.CurrentFolder);
                FilterChanged = false;
            }
        }

        private void Undo(object sender, RoutedEventArgs e)
        {
            int index = fileList.SelectedIndex;
            viewModel.Undo();
            fileList.SelectedIndex = index;
            UndoButton.IsEnabled = false;
            RedoButton.IsEnabled = true;
        }

        private void Redo(object sender, RoutedEventArgs e)
        {
            int index = fileList.SelectedIndex;
            viewModel.Redo();
            fileList.SelectedIndex = index;
            UndoButton.IsEnabled = true;
            RedoButton.IsEnabled = false;
        }

        private void NewCatName_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                NewCategoryAddButton_Click(sender, e);
            }
        }

        private void PathBox_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                PathButton_Click(sender, e);
            }
        }

        private void PathButton_Click(object sender, RoutedEventArgs e)
        {
            pathBoxFlyout.Hide();
            viewModel.SetFolder(PathBox.Text);
            if (viewModel.CurrentFolder != null)
            {
                FolderChanged();
            }
        }

        private void PathBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            PathButton.IsEnabled = PathBox.Text.Length != 0;
        }

        private void NotifCloseButton_Click(object sender, RoutedEventArgs e)
        {
            NotificationBox.Visibility = Visibility.Collapsed;
            _ = fileList.Focus(FocusState.Programmatic);
        }

        private void ResetDialogs_Click(object sender, RoutedEventArgs e)
        {
            viewModel.ShowMoveDialog = true;
            viewModel.MoveConflictOption = 0;
        }

        private void ShowNotificationBox(bool error = false, string fileName = "")
        {
            if (error)
            {
                NotificationBoxIcon.Glyph = "&#xE783;";
                NotificationBoxIcon.Foreground = new SolidColorBrush(Colors.Red);
                NotificationBoxText.Text = fileName.Length == 0
                    ? "That image already exists in the selected folder."
                    : fileName + " already exists in the selected folder";
            }
            else
            {
                NotificationBoxIcon.Glyph = "&#xE7BA;";
                NotificationBoxIcon.Foreground = new SolidColorBrush(Colors.Yellow);
                NotificationBoxText.Text = fileName.Length == 0
                    ? "File moved with conflicts."
                    : fileName + " was renamed and moved.";
            }
            NotificationBox.Visibility = Visibility.Visible;

            // Auto-close the box after 30 sec.
            DispatcherTimer timer = new();
            timer.Tick += (object sender, object e) =>
            {
                NotificationBox.Visibility = Visibility.Collapsed;
                timer.Stop();
            };
            timer.Interval = new TimeSpan(0, 0, 0, 30);
            timer.Start();
        }

        private void Menu_Click(object sender, RoutedEventArgs e)
        {
            MainView.IsPaneOpen = !MainView.IsPaneOpen;
        }

        private void FileList_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Handled == false)
            {
                switch (e.Key)
                {
                    case VirtualKey.Left:
                        if (fileList.SelectedIndex > 0)
                        {
                            fileList.SelectedIndex--;
                            e.Handled = true;
                        }
                        return;
                    case VirtualKey.Right:
                        if (fileList.SelectedIndex < fileList.Items.Count)
                        {
                            fileList.SelectedIndex++;
                            e.Handled = true;
                        }
                        return;
                    case VirtualKey.Delete:
                        if (fileList.SelectedIndex != -1)
                        {
                            viewModel.DeleteFile(viewModel.CurrentFile);
                            e.Handled = true;
                        }
                        return;
                    case VirtualKey.Number1:
                        if ((InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down
                            && viewModel.Categories.ElementAt(0) != null)
                        {
                            MoveTo(viewModel.Categories.ElementAt(0));
                            e.Handled = true;
                        }
                        return;
                    case VirtualKey.Number2:
                        if ((InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down
                            && viewModel.Categories.ElementAt(1) != null)
                        {
                            MoveTo(viewModel.Categories.ElementAt(1));
                            e.Handled = true;
                        }
                        return;
                    case VirtualKey.Number3:
                        if ((InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down
                            && viewModel.Categories.ElementAt(2) != null)
                        {
                            MoveTo(viewModel.Categories.ElementAt(2));
                            e.Handled = true;
                        }
                        return;
                    case VirtualKey.Number4:
                        if ((InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down
                            && viewModel.Categories.ElementAt(3) != null)
                        {
                            MoveTo(viewModel.Categories.ElementAt(3));
                            e.Handled = true;
                        }
                        return;
                    case VirtualKey.Number5:
                        if ((InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down
                            && viewModel.Categories.ElementAt(4) != null)
                        {
                            MoveTo(viewModel.Categories.ElementAt(4));
                            e.Handled = true;
                        }
                        return;
                    default:
                        break;
                }
            }
        }

        private void ImagePreview_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (viewModel.CurrentFile != null)
            {
                ImageInfoBar.Visibility = Visibility.Visible;
            }
        }

        private void ImagePreview_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (viewModel.CurrentFile != null)
            {
                ImageInfoBar.Visibility = Visibility.Collapsed;
            }
        }

        private void DeleteImageButton_Click(object sender, RoutedEventArgs e)
        {
            viewModel.DeleteFile(viewModel.CurrentFile);
        }

        private void ImageInfoButton_Click(object sender, RoutedEventArgs e)
        {
            ImageSplit.IsPaneOpen = true;
        }

        private async void AboutItem_Click(object sender, RoutedEventArgs e)
        {
            ContentDialog aboutDialog = new()
            {
                XamlRoot = XamlRoot,
                Title = "(New) Picture Sorter",
                CloseButtonText = "Ok",
                DefaultButton = ContentDialogButton.Close
            };

            StackPanel versionInfo = new()
            {
                Orientation = Orientation.Vertical
            };
            versionInfo.Children.Add(new TextBlock()
            {
                Text = string.Format("Version: {0}.{1}.{2}.{3}",
                    Package.Current.Id.Version.Major,
                    Package.Current.Id.Version.Minor,
                    Package.Current.Id.Version.Build,
                    Package.Current.Id.Version.Revision)
            });
            aboutDialog.Content = versionInfo;

            await aboutDialog.ShowAsync();
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Exit();
        }

        private void OpenFolderQuickly_Click(object sender, RoutedEventArgs e)
        {
            pathBoxFlyout.ShowAt(PickFolderButton);
        }

        //private async void TextBox_KeyUp(object sender, KeyRoutedEventArgs e)
        //{
        //    TextBox box = sender as TextBox;
        //    string tag = box.Tag as string;
        //    if (e.Key == VirtualKey.Enter)
        //    {
        //        if (tag.Contains("Artist"))
        //        {
        //            List<KeyValuePair<string, object>> propertyToSave = new()
        //            {
        //                new KeyValuePair<string, object>("System.Author", box.Text)
        //            };

        //            await viewModel.CurrentFile.Properties.SavePropertiesAsync(propertyToSave);
        //        }
        //        else if (tag.Contains("Title"))
        //        {
        //            List<KeyValuePair<string, object>> propertyToSave = new()
        //            {
        //                new KeyValuePair<string, object>("System.Title", box.Text),
        //                new KeyValuePair<string, object>("System.Subject", box.Text)
        //            };

        //            await viewModel.CurrentFile.Properties.SavePropertiesAsync(propertyToSave);
        //        }
        //        else if (tag.Contains("Keyword"))
        //        {
        //            ImageProperties imageProps = await viewModel.CurrentFile.Properties.GetImagePropertiesAsync();
        //            string[] keywords = box.Text.Split(",");
        //            for (int i = 0; i < keywords.Length; i++)
        //            {
        //                imageProps.Keywords.Add(keywords[i]);
        //            }
        //            await imageProps.SavePropertiesAsync();
        //        }
        //    }
        //}
    }
}
