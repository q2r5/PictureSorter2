using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Runtime.InteropServices;
using Windows.Storage;
using Windows.Storage.Pickers;
using Microsoft.UI.Xaml.Input;
using System.Linq;
using System.Numerics;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;

#if !UNIVERSAL
using WinRT;
#endif

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace App1
{
    [ComImport, Guid("3E68D4BD-7135-4D10-8018-9FB6D9F33FA1"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IInitializeWithWindow
    {
        void Initialize([In] IntPtr hwnd);
    }

    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private readonly MainViewModel viewModel = MainViewModel.Instance;

        private bool FilterChanged;

        private readonly string Title = "(New) Picture Sorter";

        public MainPage()
        {
            InitializeComponent();
            App.CurrentWindow.SetTitleBar(AppTitleBar);
            App.CurrentWindow.Title = Title;

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

            NotificationBox.Translation += new Vector3(0, 0, 32);
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
            if (App.CurrentWindow != null)
            {
                IInitializeWithWindow initializeWithWindowWrapper = folderPicker.As<IInitializeWithWindow>();
                initializeWithWindowWrapper.Initialize(App.HWND);
            }
#endif

            viewModel.CurrentFolder = await folderPicker.PickSingleFolderAsync();
        }

        public void FolderChanged()
        {
            RefreshButton.IsEnabled = true;
            ConvertButton.IsEnabled = true;
            NewCategoryButton.IsEnabled = true;
            ImagePreview.Source = null;
        }

        private void FileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
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

        private async void CategoryGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            try
            {
                await viewModel.MoveFile(viewModel.CurrentFile, e.ClickedItem as StorageFolder);
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
                            await viewModel.MoveFile(viewModel.CurrentFile, e.ClickedItem as StorageFolder, NameCollisionOption.GenerateUniqueName);
                            ShowNotificationBox();
                            UndoButton.IsEnabled = true;
                            break;
                        case ContentDialogResult.None:
                            ShowNotificationBox(true);
                            break;
                        case ContentDialogResult.Secondary:
                            await viewModel.MoveFile(viewModel.CurrentFile, e.ClickedItem as StorageFolder, NameCollisionOption.ReplaceExisting);
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
                        await viewModel.MoveFile(currentFile, e.ClickedItem as StorageFolder, NameCollisionOption.GenerateUniqueName);
                        ShowNotificationBox();
                    }
                    else if (viewModel.MoveConflictOption == 2)
                    {
                        await viewModel.MoveFile(viewModel.CurrentFile, e.ClickedItem as StorageFolder, NameCollisionOption.ReplaceExisting);
                        ShowNotificationBox(true);
                    }

                    ShowNotificationBox();
                    UndoButton.IsEnabled = true;
                }
            }
            finally
            {
                fileList.Focus(FocusState.Programmatic);
            }

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
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                NewCategoryAddButton_Click(sender, e);
            }
        }

        private void PathBox_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                PathButton_Click(sender, e);
            }
        }

        private void PathButton_Click(object sender, RoutedEventArgs e)
        {
            FolderHeader.ContextFlyout.Hide();
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
            fileList.Focus(FocusState.Programmatic);
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

        private async void FileList_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Left && fileList.SelectedIndex > 0)
            {
                fileList.SelectedIndex -= 1;
            }
            else if (e.Key == Windows.System.VirtualKey.Right && fileList.SelectedIndex < fileList.Items.Count)
            {
                fileList.SelectedIndex += 1;
            } else if (e.Key == Windows.System.VirtualKey.Delete && fileList.SelectedIndex != -1)
            {
                await viewModel.CurrentFile.DeleteAsync();
            }
        }
    }
}
