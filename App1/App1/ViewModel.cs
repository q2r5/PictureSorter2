using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Search;

namespace App1
{
    class MainViewModel
    {
        private readonly ApplicationDataContainer appData = ApplicationData.Current.LocalSettings;

        public StorageFolder CurrentFolder
        {
            get => _currentFolder;
            set
            {
                if (_currentFolder != value)
                {
                    appData.Values["path"] = value.Path;
                    _currentFolder = value;
                    ChangeFolderAsync();
                }
            }
        }
        private StorageFolder _currentFolder;

        private HashSet<string> FilteredFileTypes
        {
            get => _filteredFileTypes;
            set
            {
                if (_filteredFileTypes != value)
                {
                    appData.Values["fileTypes"] = string.Join(",", value);
                    _filteredFileTypes = value;
                }
            }
        }
        private HashSet<string> _filteredFileTypes;

        private ObservableCollection<StorageFile> files;
        private ObservableCollection<StorageFolder> folders;
        private StorageFile currentFile;

        public async Task ChangeFolderAsync()
        {
            StorageApplicationPermissions.FutureAccessList.AddOrReplace("PickedFolderToken", CurrentFolder);
            files = await GetFiles(CurrentFolder);
            folders = await GetFolders(CurrentFolder);
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

    }
}
