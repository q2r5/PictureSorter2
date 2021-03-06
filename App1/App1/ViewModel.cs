using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.FileProperties;
using Windows.Storage.Search;
using Windows.Storage.Streams;

namespace App1
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public static MainViewModel Instance { get; private set; }

        #region Variables
        private readonly ApplicationDataContainer appData = ApplicationData.Current.LocalSettings;

        private readonly string AppTitle = "(New) Picture Sorter";
        public string Title {
            get => _title;
            set
            {
                string newTitle;
                if (value != null)
                {
                    newTitle = value + " - " + AppTitle;
                }
                else
                {
                    newTitle = AppTitle;
                }

                _ = SetProperty(ref _title, newTitle);
            }
        }
        private string _title = "(New) Picture Sorter";

        public StorageFolder CurrentFolder
        {
            get => _currentFolder;
            set
            {
                if (value != null && SetProperty(ref _currentFolder, value))
                {
                    StorageApplicationPermissions.FutureAccessList.AddOrReplace("PickedFolderToken", value);
                    appData.Values["path"] = value.Path;
                    if ((App.CurrentWindow.ContentFrame.Content as MainPage) != null)
                    {
                        (App.CurrentWindow.ContentFrame.Content as MainPage).FolderChanged();
                    }
                    Title = value.DisplayName;
                    GetFiles(value, true);
                    GetFolders(value);
                }
            }
        }
        private StorageFolder _currentFolder;

        public StorageFile CurrentFile
        {
            get => _currentFile;
            set => SetProperty(ref _currentFile, value);

        }
        private StorageFile _currentFile;

        public ObservableCollection<StorageFile> CurrentFiles
        {
            get => _currentFiles;
            set => SetProperty(ref _currentFiles, value);
        }
        private ObservableCollection<StorageFile> _currentFiles = new();

        public HashSet<StorageFolder> Categories
        {
            get => _categories;
            set => SetProperty(ref _categories, value);
        }
        private HashSet<StorageFolder> _categories = new();

        public HashSet<string> FilteredFileTypes
        {
            get => _filteredFileTypes;
            set
            {
                if (SetProperty(ref _filteredFileTypes, value))
                {
                    if (CurrentFolder != null)
                    {
                        GetFiles(CurrentFolder);
                    }
                }
            }
        }
        private HashSet<string> _filteredFileTypes = new();

        public bool? ShowMoveDialog
        {
            get => (bool?)appData.Values["HideMoveDialog"];
            set => appData.Values["HideMoveDialog"] = value;
        }

        public int? MoveConflictOption
        {
            get => (int?)appData.Values["MoveConflictOption"];
            set
            {
                if (value <= 2)
                {
                    appData.Values["MoveConflictOption"] = value;
                }
            }
        }

        public bool UseSearchIndexer
        {
            get => (bool?)appData.Values["UseSearchIndexer"] ?? true;
            set => appData.Values["UseSearchIndexer"] = value;
        }

        public bool UseTitlebar
        {
            get => (bool?)appData.Values["UseTitlebar"] ?? true;
            set => appData.Values["UseTitlebar"] = value;
        }

        private (StorageFile, StorageFolder, int) lastCommand;
        #endregion

        #region Initializers
        static MainViewModel()
        {
            Instance = new MainViewModel();
        }

        private MainViewModel()
        {
            FilteredFileTypes = ((string)appData.Values["fileTypes"] ?? ".jpg,.png,.gif").Split(",").ToHashSet();
            if (MoveConflictOption == null)
            {
                MoveConflictOption = 0;
            }
            SetFolder(appData.Values["path"] as string);
        }
        #endregion

        public async void SetFolder(string path)
        {
            if (path == null) { return; }
            CurrentFolder = await StorageFolder.GetFolderFromPathAsync(path);
        }

        public async void GetFiles(StorageFolder folder, bool folderChanged = false)
        {
            QueryOptions query = new(CommonFileQuery.OrderByName, fileTypeFilter: FilteredFileTypes)
            {
                FolderDepth = FolderDepth.Shallow,
                IndexerOption = UseSearchIndexer ? IndexerOption.UseIndexerWhenAvailable : IndexerOption.DoNotUseIndexer
            };
            StorageFileQueryResult result = folder.CreateFileQueryWithOptions(query);
            IReadOnlyList<StorageFile> files = await result.GetFilesAsync();
            List<StorageFile> fileList = files.ToList();
            if (CurrentFiles == null)
            {
                CurrentFiles = new(files);
                return;
            }
            else
            {
                if (folderChanged)
                {
                    CurrentFiles.Clear();
                }

                if (CurrentFiles.Count > 0)
                {
                    List<StorageFile> newFiles = fileList.Except(CurrentFiles, new StorageFileEqualityComparer()).ToList();
                    List<StorageFile> removedFiles = CurrentFiles.Except(files, new StorageFileEqualityComparer()).ToList();
                    if (newFiles.Count > 0)
                    {
                        newFiles.ForEach(f =>
                        {
                            int index = fileList.IndexOf(f);
                            CurrentFiles.Insert(index, f);
                        });
                    }
                    
                    if (removedFiles.Count > 0)
                    {
                        removedFiles.ForEach(f =>
                        {
                            _ = CurrentFiles.Remove(f);
                        });
                    }
                }
                else
                {
                    fileList.ForEach(f => CurrentFiles.Add(f));
                }
            }
        }

        public void SaveFilterList()
        {
            appData.Values["fileTypes"] = string.Join(",", FilteredFileTypes);
        }

        public async void GetFolders(StorageFolder folder)
        {
            IReadOnlyList<StorageFolder> folders = await folder.GetFoldersAsync();
            Categories = new(folders);
        }

        public async Task MoveFile(StorageFile file, StorageFolder folder, NameCollisionOption collisionOption = NameCollisionOption.FailIfExists)
        {
            if (file == null)
            {
                file = CurrentFile;
            }
            if (file == null || folder == null) { throw new ArgumentNullException(message: "file or folder was null", null); }

            try
            {
                await file.MoveAsync(folder, file.Name, collisionOption);
            }
            catch
            {
                throw;
            }
            finally
            {
                int idx = CurrentFiles.IndexOf(file);
                _ = CurrentFiles.Remove(file);
                lastCommand = (file, folder, idx);
            }
        }

        public async void DeleteFile(StorageFile file)
        {
            if (file == null) { throw new ArgumentNullException(message: "File is null", null); }

            await file.DeleteAsync();
            _ = CurrentFiles.Remove(file);
            lastCommand = (null, null, -1);
        }

        public async Task<Dictionary<string, string>> GetFileInfo(StorageFile file = null)
        {
            if (file == null && CurrentFile != null)
            {
                file = CurrentFile;
            }

            // If `file` is still null, bail.
            if (file == null)
            {
                return null;
            }

            ImageProperties imageProps = await file.Properties.GetImagePropertiesAsync();
            string imageSize = string.Format("{0} x {1}", imageProps.Height, imageProps.Width);

            BasicProperties basicProps = await file.GetBasicPropertiesAsync();
            string fileSize = string.Format(new FileSizeFormatProvider(), "{0:fs}", basicProps.Size);

            Dictionary<string, string> outputList = new();
            outputList.Add("Created:", file.DateCreated.DateTime.ToString("g"));
            outputList.Add("File Size:", fileSize);
            outputList.Add("Image Size:", imageSize);

            return outputList;
        }

        public async Task<Dictionary<string, string>> GetEditableFileInfo(StorageFile file = null)
        {
            if (file == null && CurrentFile != null)
            {
                file = CurrentFile;
            }

            // If `file` is still null, bail.
            if (file == null)
            {
                return null;
            }

            ImageProperties imageProps = await file.Properties.GetImagePropertiesAsync();
            DocumentProperties docProps = await file.Properties.GetDocumentPropertiesAsync();

            Dictionary<string, string> outputList = new();

            string keywords = string.Join(",", imageProps.Keywords);
            string artists = string.Join(",", docProps.Author);

            outputList.Add("Title:", imageProps.Title);
            outputList.Add("Artist(s)", artists);
            outputList.Add("Keywords", keywords);

            return outputList;
        }

        public async void NewFolder(string name, StorageFolder folder)
        {
            if (name == null || folder == null) { return; }
            _ = await folder.CreateFolderAsync(name);
            GetFolders(folder);
        }

        public async void ConvertFile(StorageFile file, string fileType)
        {
            Guid encoderID = new();
            string extension = "";

            if (fileType == "PNG")
            {
                encoderID = BitmapEncoder.PngEncoderId;
                extension = ".png";
            }
            else if (fileType == "JPEG")
            {
                encoderID = BitmapEncoder.JpegEncoderId;
                extension = ".jpg";
            }
            else if (fileType == "GIF")
            {
                encoderID = BitmapEncoder.GifEncoderId;
                extension = ".gif";
            }
            else if (fileType == "HEIF")
            {
                encoderID = BitmapEncoder.HeifEncoderId;
                extension = ".heif";
            }
            else if (fileType == "JXR")
            {
                encoderID = BitmapEncoder.JpegXREncoderId;
                extension = ".jxr";
            }

            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(await file.OpenReadAsync());
            SoftwareBitmap bitmap = await decoder.GetSoftwareBitmapAsync();

            await file.RenameAsync(file.DisplayName + ".bak", NameCollisionOption.GenerateUniqueName);
            StorageFile newFile = await CurrentFolder.CreateFileAsync(file.DisplayName.Split(".")[0] + extension);

            IRandomAccessStream stream = await newFile.OpenAsync(FileAccessMode.ReadWrite);
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

        public async void Undo()
        {
            if (CurrentFiles.Contains(lastCommand.Item1)) { return; }
            await MoveFile(lastCommand.Item1, CurrentFolder);
            CurrentFiles.Insert(lastCommand.Item3, lastCommand.Item1);
        }

        public async void Redo()
        {
            await MoveFile(lastCommand.Item1, lastCommand.Item2);
            GetFiles(CurrentFolder);
        }

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged = delegate { };

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value))
            {
                return false;
            }

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }

    internal class StorageFileEqualityComparer : IEqualityComparer<StorageFile>
    {
        public bool Equals(StorageFile x, StorageFile y)
        {
            return x.IsEqual(y);
        }

        public int GetHashCode([DisallowNull] StorageFile obj)
        {
            return obj.DisplayName.GetHashCode();
        }
    }
}