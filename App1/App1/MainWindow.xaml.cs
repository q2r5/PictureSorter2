using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel.Contacts;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.Storage.Search;

#if !UNIVERSAL
using WinRT;
#endif

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace App1
{
    [ComImport, System.Runtime.InteropServices.Guid("3E68D4BD-7135-4D10-8018-9FB6D9F33FA1"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
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
            get { return _currentFolder; }
            set
            {
                if (_currentFolder != value)
                {
                    _currentFolder = value;
                    FolderChanged();
                }
            }
        }

        private StorageFolder _currentFolder;

        List<string> filteredFileTypes;
        private ObservableCollection<StorageFile> files;
        private ObservableCollection<StorageFolder> folders;
        private StorageFile currentFile;

        public MainWindow()
        {
            this.InitializeComponent();
        }

        private async void PickFolderButton_Click(object sender, RoutedEventArgs e)
        {
            FolderPicker folderPicker = new()
            {
                SuggestedStartLocation = PickerLocationId.Desktop
            };

            filteredFileTypes = filterTypesBox.Text.Split(",").ToList();

            foreach (string fileType in filteredFileTypes)
            {
                folderPicker.FileTypeFilter.Add(fileType);
            }

#if !UNIVERSAL
            // When running on win32, FileOpenPicker needs to know the top-level hwnd via IInitializeWithWindow::Initialize.
            if (Window.Current == null)
            {
                IInitializeWithWindow initializeWithWindowWrapper = folderPicker.As<IInitializeWithWindow>();
                IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                initializeWithWindowWrapper.Initialize(hwnd);
            }
#endif

            CurrentFolder = await folderPicker.PickSingleFolderAsync();
        }

//        private async void PickFile()
//        {
//            FileOpenPicker filePicker = new()
//            {
//                SuggestedStartLocation = PickerLocationId.Desktop
//            };
//            filteredFileTypes = filterTypesBox.Text.Split(",").ToList();
//            foreach (string fileType in filteredFileTypes)
//            {
//                filePicker.FileTypeFilter.Add(fileType);
//            }

//#if !UNIVERSAL
//            // When running on win32, FileOpenPicker needs to know the top-level hwnd via IInitializeWithWindow::Initialize.
//            if (Window.Current == null)
//            {
//                IInitializeWithWindow initializeWithWindowWrapper = filePicker.As<IInitializeWithWindow>();
//                IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
//                initializeWithWindowWrapper.Initialize(hwnd);
//            }
//#endif

//            currentFile = await filePicker.PickSingleFileAsync();
//            Uri uri = new(currentFile.Path);
//            BitmapImage image = new(uri);
//            ImagePreview.Source = image;
//        }

        private async void FolderChanged()
        {
            if (CurrentFolder == null) { return; }

            StorageApplicationPermissions.FutureAccessList.AddOrReplace("PickedFolderToken", CurrentFolder);
            filePathBox.Text = CurrentFolder.Path;

            files = await GetFiles(CurrentFolder, filteredFileTypes);
            folders = await GetFolders(CurrentFolder);

            MoveButton.IsEnabled = true;
            RefreshFilesButton.IsEnabled = true;

            fileList.ItemsSource = files;
            fileList.SelectedIndex = 0;

            FolderList.ItemsSource = folders;
            FolderList.DisplayMemberPath = "Name";
            FolderList.SelectedIndex = 0;
        }

        private async void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            filteredFileTypes = filterTypesBox.Text.Split(",").ToList();
            files = await GetFiles(CurrentFolder, filteredFileTypes);
        }

        private async Task<ObservableCollection<StorageFile>> GetFiles(StorageFolder folder, List<string> filteredFileTypes)
        {
            QueryOptions query = new(CommonFileQuery.OrderByName, filteredFileTypes);
            query.FolderDepth = FolderDepth.Shallow;
            query.IndexerOption = IndexerOption.UseIndexerWhenAvailable;
            StorageFileQueryResult result = folder.CreateFileQueryWithOptions(query);
            IReadOnlyList<StorageFile> files = await result.GetFilesAsync();

            var fileList = new ObservableCollection<StorageFile>();

            for (int i = 0; i < files.Count; i += 1)
            {
                fileList.Add(files[i]);
            }

            return fileList;
        }

        private async Task<ObservableCollection<StorageFolder>> GetFolders(StorageFolder folder)
        {
            StorageFolderQueryResult result = folder.CreateFolderQuery();
            IReadOnlyList<StorageFolder> folders = await result.GetFoldersAsync();
            var folderList = new ObservableCollection<StorageFolder>();
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
        }

        private async void MoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (fileList.SelectedIndex == -1 || FolderList.SelectedIndex == -1) { return; }

            var folder = folders[FolderList.SelectedIndex];

            var selectedIndex = fileList.SelectedIndex;

            await currentFile.MoveAsync(folder);
            files.RemoveAt(selectedIndex);
            fileList.SelectedIndex = selectedIndex;
        }

        private async void RefreshFilesButton_Click(object sender, RoutedEventArgs e)
        {
            files = await GetFiles(CurrentFolder, filteredFileTypes);
            fileList.ItemsSource = files;
            fileList.SelectedIndex = 0;
        }
    }
}
