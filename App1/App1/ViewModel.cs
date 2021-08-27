﻿using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Search;
using Windows.Storage.Streams;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;

namespace App1
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public static MainViewModel Instance { get; private set; }

        private readonly ApplicationDataContainer appData = ApplicationData.Current.LocalSettings;

        public StorageFolder CurrentFolder
        {
            get => _currentFolder;
            set
            {
                if (SetProperty(ref _currentFolder, value))
                {
                    StorageApplicationPermissions.FutureAccessList.AddOrReplace("PickedFolderToken", value);
                    appData.Values["path"] = value.Path;
                    GetFiles(value);
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
                    appData.Values["fileTypes"] = string.Join(",", value);
                }
            }
        }
        private HashSet<string> _filteredFileTypes = new();

        private (StorageFile, StorageFolder) lastCommand;

        static MainViewModel()
        {
            Instance = new MainViewModel();
        }

        private MainViewModel()
        {
            FilteredFileTypes = ((string)appData.Values["fileTypes"] ?? ".jpg,.png,.gif").Split(",").ToHashSet();
            SetFolder(appData.Values["path"] as string);
        }

        public async void SetFolder(string path)
        {
            if (path == null) { return; }
            CurrentFolder = await StorageFolder.GetFolderFromPathAsync(path);
        }

        public async void GetFiles(StorageFolder folder)
        {
            CurrentFolder = folder;
            QueryOptions query = new(CommonFileQuery.OrderByName, fileTypeFilter: FilteredFileTypes)
            {
                FolderDepth = FolderDepth.Shallow,
                IndexerOption = IndexerOption.UseIndexerWhenAvailable
            };
            StorageFileQueryResult result = folder.CreateFileQueryWithOptions(query);
            IReadOnlyList<StorageFile> files = await result.GetFilesAsync();
            if (CurrentFiles == null)
            {
                CurrentFiles = new(files);
                return;
            }
            else
            {
                if (CurrentFiles.Count > 0)
                {
                    CurrentFiles.Clear();
                }
                files.ToList().ForEach(f => CurrentFiles.Add(f));
            }
        }

        public async void GetFolders(StorageFolder folder)
        {
            IReadOnlyList<StorageFolder> folders = await folder.GetFoldersAsync();
            Categories = folders.ToHashSet();
        }

        public async void MoveFile(StorageFile file, StorageFolder folder)
        {
            if (file == null || folder == null) { return; }
            await file.MoveAsync(folder, file.Name, NameCollisionOption.GenerateUniqueName);
            _ = CurrentFiles.Remove(file);

            lastCommand = (file, folder);
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

        public void Undo()
        {
            if (CurrentFiles.Contains(lastCommand.Item1)) { return; }
            MoveFile(lastCommand.Item1, CurrentFolder);
        }

        public void Redo()
        {
            MoveFile(lastCommand.Item1, lastCommand.Item2);
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
}
