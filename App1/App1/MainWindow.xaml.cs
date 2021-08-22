using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.Storage.Search;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Microsoft.UI.Xaml.Input;

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
        private StorageFolder CurrentFolder
        {
            get => _currentFolder;
            set
            {
                if (_currentFolder != value)
                {
                    appData.Values["path"] = value.Path;
                    _currentFolder = value;
                    FolderChanged();
                }
            }
        }

        private StorageFolder _currentFolder;
        private bool FilterChanged;
        private HashSet<string> FilteredFileTypes
        {
            get => _filteredFileTypes;
            set
            {
                if (_filteredFileTypes != value)
                {
                    appData.Values["fileTypes"] = string.Join(",", value);
                    _filteredFileTypes = value;
                    FilterChanged = true;
                }
            }
        }
        private HashSet<string> _filteredFileTypes;
        private ObservableCollection<StorageFile> files;
        private ObservableCollection<StorageFolder> folders;
        private StorageFile currentFile;

        private readonly ApplicationDataContainer appData = ApplicationData.Current.LocalSettings;

        private HashSet<(StorageFile, StorageFolder)> recentCommands = new();

        public MainWindow()
        {
            InitializeComponent();

            FilteredFileTypes = ((string)appData.Values["fileTypes"] ?? ".jpg,.png,.gif").Split(",").ToHashSet();
            FilterChanged = false;
            foreach (string fileType in FilteredFileTypes)
            {
                foreach (MenuFlyoutItemBase item in FilterMenu.Items)
                {
                    if (item.Tag != null && item.Tag.ToString() == fileType)
                    {
                        (item as ToggleMenuFlyoutItem).IsChecked = true;
                    }
                }
            }

            if (appData.Values["path"] != null)
            {
                CurrentFolder = StorageFolder.GetFolderFromPathAsync((string)appData.Values["path"]).GetAwaiter().GetResult();
            }

            _ = fileList.Focus(FocusState.Programmatic);
        }

        private async void PickFolderButton_Click(object sender, RoutedEventArgs e)
        {
            FolderPicker folderPicker = new()
            {
                SuggestedStartLocation = PickerLocationId.Desktop
            };

            foreach (string fileType in FilteredFileTypes)
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

            CurrentFolder = await folderPicker.PickSingleFolderAsync();
        }

        private async void FolderChanged()
        {
            if (CurrentFolder == null) { return; }

            StorageApplicationPermissions.FutureAccessList.AddOrReplace("PickedFolderToken", CurrentFolder);

            files = await GetFiles(CurrentFolder);
            folders = await GetFolders(CurrentFolder);

            MoveButton.IsEnabled = true;
            RefreshCategoriesButton.IsEnabled = true;
            RefreshFilesButton.IsEnabled = true;
            ConvertButton.IsEnabled = true;
            NewCategoryButton.IsEnabled = true;

            fileList.ItemsSource = files;
            fileList.SelectedIndex = 0;

            FolderList.ItemsSource = folders;
            FolderList.DisplayMemberPath = "Name";
            FolderList.SelectedIndex = 0;

            CategoryGrid.ItemsSource = folders;

            FolderHeader.Text = CurrentFolder.DisplayName;
        }

        private async Task<ObservableCollection<StorageFile>> GetFiles(StorageFolder folder)
        {
            QueryOptions query = new(CommonFileQuery.OrderByName, fileTypeFilter: FilteredFileTypes);
            query.FolderDepth = FolderDepth.Shallow;
            query.IndexerOption = IndexerOption.UseIndexerWhenAvailable;
            StorageFileQueryResult result = folder.CreateFileQueryWithOptions(query);
            IReadOnlyList<StorageFile> files = await result.GetFilesAsync();

            ObservableCollection<StorageFile> fileList = new();

            for (int i = 0; i < files.Count; i += 1)
            {
                fileList.Add(files[i]);
            }

            return fileList;
        }

        private static async Task<ObservableCollection<StorageFolder>> GetFolders(StorageFolder folder)
        {
            StorageFolderQueryResult result = folder.CreateFolderQuery();
            IReadOnlyList<StorageFolder> folders = await result.GetFoldersAsync();
            ObservableCollection<StorageFolder> folderList = new();
            for (int i = 0; i < folders.Count; i += 1)
            {
                folderList.Add(folders[i]);
            }

            return folderList;
        }

        private void FileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (fileList.SelectedIndex == -1) { return; }

            currentFile = files[fileList.SelectedIndex];
            Uri uri = new(currentFile.Path);
            BitmapImage image = new(uri);
            ImagePreview.Source = image;

            string fileType = currentFile.FileType.ToUpperInvariant().Replace(".", "");
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

        private async void MoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (fileList.SelectedIndex == -1 || FolderList.SelectedIndex == -1) { return; }

            StorageFolder folder = folders[FolderList.SelectedIndex];

            await MoveFile(currentFile, folder);
        }

        private async void RefreshFilesButton_Click(object sender, RoutedEventArgs e)
        {
            files = await GetFiles(CurrentFolder);
            fileList.ItemsSource = files;
            fileList.SelectedIndex = 0;
        }

        private async void ConvertOption_Click(object sender, RoutedEventArgs e)
        {
            string option = ((MenuFlyoutItem)sender).Tag.ToString();
            string extension = "";
            Guid encoderID = new();

            if (option == "PNG")
            {
                encoderID = BitmapEncoder.PngEncoderId;
                extension = ".png";
            }
            else if (option == "JPEG")
            {
                encoderID = BitmapEncoder.JpegEncoderId;
                extension = ".jpg";
            }
            else if (option == "GIF")
            {
                encoderID = BitmapEncoder.GifEncoderId;
                extension = ".gif";
            }
            else if (option == "HEIF")
            {
                encoderID = BitmapEncoder.HeifEncoderId;
                extension = ".heif";
            } else if (option == "JXR")
            {
                encoderID = BitmapEncoder.JpegXREncoderId;
                extension = ".jxr";
            }

            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(await currentFile.OpenReadAsync());
            SoftwareBitmap bitmap = await decoder.GetSoftwareBitmapAsync();

            await currentFile.RenameAsync(currentFile.DisplayName + ".bak", NameCollisionOption.GenerateUniqueName);
            StorageFile newFile = await CurrentFolder.CreateFileAsync(currentFile.DisplayName.Split(".")[0] + extension);

            using IRandomAccessStream stream = await newFile.OpenAsync(FileAccessMode.ReadWrite);
            BitmapEncoder encoder = await BitmapEncoder.CreateAsync(encoderID, stream);
            encoder.SetSoftwareBitmap(bitmap);
            encoder.IsThumbnailGenerated = true;

            try
            {
                await encoder.FlushAsync();
            }
            catch (Exception err)
            {
                const int WINCODEC_ERR_UNSUPPORTED_OPERATION = unchecked((int)0x88982F81);
                switch (err.HResult)
                {
                    case WINCODEC_ERR_UNSUPPORTED_OPERATION:
                        encoder.IsThumbnailGenerated = false;
                        break;
                    default:
                        throw;
                }
            }

            if (encoder.IsThumbnailGenerated == false)
            {
                await encoder.FlushAsync();
            }
        }

        private async void NewCategoryButton_Click(object sender, RoutedEventArgs e)
        {
            NewCategoryButton.Flyout.Hide();
            _ = await CurrentFolder.CreateFolderAsync(NewCatName.Text, CreationCollisionOption.GenerateUniqueName);
            folders = await GetFolders(CurrentFolder);

            FolderList.ItemsSource = folders;
            FolderList.DisplayMemberPath = "Name";
            FolderList.SelectedIndex = 0;

            CategoryGrid.ItemsSource = folders;
        }

        private void NewCatName_TextChanged(object sender, TextChangedEventArgs e)
        {
            NewCatAddButton.IsEnabled = NewCatName.Text.Length != 0;
        }

        private async void CategoryGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            await MoveFile(currentFile, e.ClickedItem as StorageFolder);
        }

        private async Task MoveFile(StorageFile file, StorageFolder folder)
        {
            if (file == null || folder == null || fileList.SelectedIndex == -1) { return; }
            int selectedIndex = fileList.SelectedIndex;
            await file.MoveAsync(folder);
            _ = files.Remove(file);
            fileList.SelectedIndex = selectedIndex;
            _ = recentCommands.Add((file, folder));
        }

        private async void RefreshCategoriesButton_Click(object sender, RoutedEventArgs e)
        {
            folders = await GetFolders(CurrentFolder);

            FolderList.ItemsSource = folders;
            FolderList.DisplayMemberPath = "Name";
            FolderList.SelectedIndex = 0;

            CategoryGrid.ItemsSource = folders;
        }

        private void ToggleMenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            ToggleMenuFlyoutItem item = sender as ToggleMenuFlyoutItem;
            string optionTag = item.Tag.ToString();

            _ = item.IsChecked ? FilteredFileTypes.Add(optionTag) : FilteredFileTypes.Remove(optionTag);
        }

        private async void FilterMenu_Closed(object sender, object e)
        {
            if (FilterChanged == false) { return; }
            files = await GetFiles(CurrentFolder);
            fileList.ItemsSource = files;
            fileList.SelectedIndex = 0;
        }

        private bool CanUndo()
        {
            return recentCommands != null && recentCommands.Count > 0;
        }

        private async void Undo(object sender, RoutedEventArgs e)
        {
            (StorageFile, StorageFolder) lastCommand = recentCommands.Last();
            if (files.Contains(lastCommand.Item1)) { return; }
            await MoveFile(lastCommand.Item1, CurrentFolder);
            int index = fileList.SelectedIndex;
            files.Insert(index, lastCommand.Item1);
            fileList.ItemsSource = files;
            fileList.SelectedIndex = index;
        }

        private async void Redo(object sender, RoutedEventArgs e)
        {
            (StorageFile, StorageFolder) lastCommand = recentCommands.Last();
            await MoveFile(lastCommand.Item1, lastCommand.Item2);
            files = await GetFiles(CurrentFolder);
            int index = fileList.SelectedIndex;
            fileList.ItemsSource = files;
            fileList.SelectedIndex = index;
        }

        private void NewCatName_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                NewCategoryButton_Click(sender, e);
            }
        }
    }
}
