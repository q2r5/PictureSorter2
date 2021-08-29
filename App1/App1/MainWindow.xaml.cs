﻿using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Runtime.InteropServices;
using Windows.Storage;
using Windows.Storage.Pickers;
using Microsoft.UI.Xaml.Input;
using System.Linq;

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
    public sealed partial class MainWindow : Window
    {
        private readonly MainViewModel viewModel = MainViewModel.Instance;

        private bool FilterChanged;

        public MainWindow()
        {
            InitializeComponent();

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
            if (Current == null)
            {
                IInitializeWithWindow initializeWithWindowWrapper = folderPicker.As<IInitializeWithWindow>();
                IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                initializeWithWindowWrapper.Initialize(hwnd);
            }
#endif

            viewModel.CurrentFolder = await folderPicker.PickSingleFolderAsync();
            if (viewModel.CurrentFolder != null)
            {
                FolderChanged();
            }
        }

        private void FolderChanged()
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

        private void NewCategoryButton_Click(object sender, RoutedEventArgs e)
        {
            NewCategoryButton.Flyout.Hide();
            viewModel.NewFolder(NewCatName.Text, viewModel.CurrentFolder);
        }

        private void NewCatName_TextChanged(object sender, TextChangedEventArgs e)
        {
            NewCatAddButton.IsEnabled = NewCatName.Text.Length != 0;
        }

        private void CategoryGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            viewModel.MoveFile(viewModel.CurrentFile, e.ClickedItem as StorageFolder);
            UndoButton.IsEnabled = true;
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
            viewModel.GetFiles(viewModel.CurrentFolder);
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

        private void NewCatName_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                NewCategoryButton_Click(sender, e);
            }
        }

        private void PathBox_KeyDown(object sender, KeyRoutedEventArgs e)
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
    }
}
