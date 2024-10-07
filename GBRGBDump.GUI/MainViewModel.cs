using GBTools.Bootstrapper;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using GBRGBDump.GUI.Commands;
using GBRGBDump.GUI.Services;
using GBTools.Common.Services;
using GBTools.Common;
using GBTools.Graphics;

namespace GBRGBDump.GUI
{
    public class MainViewModel : ViewModelBase
    {
        #region Binding Properties

        private string _sourcePath = string.Empty;

        public string SourcePath
        {
            get => _sourcePath;
            set
            {
                _sourcePath = value;
                _settingsService.SourcePath = value;
                OnPropertyChanged();
                UpdateStartupCondition();
            }
        }

        private string _destinationPath = string.Empty;

        public string DestinationPath
        {
            get => _destinationPath;
            set
            {
                _destinationPath = value;
                _settingsService.DestinationPath = value;
                OnPropertyChanged();
                UpdateStartupCondition();
            }
        }

        private bool _isWorking = false;

        public bool IsWorking
        {
            get => _isWorking;
            set
            {
                _isWorking = value;
                OnPropertyChanged();
                UpdateStartupCondition();
            }
        }

        private bool _doRGBMerge = false;

        public bool DoRgbMerge
        {
            get => _doRGBMerge;
            set
            {
                _doRGBMerge = value;
                _settingsService.DoRGBMerge = value;
                OnPropertyChanged();
            }
        }

        private bool _doHDR = false;

        public bool DoHDR
        {
            get => _doHDR;
            set
            {
                _doHDR = value;
                _settingsService.DoHDR = value;
                OnPropertyChanged();
            }
        }

        private bool _doFullHDR = false;

        public bool DoFullHDR
        {
            get => _doFullHDR;
            set
            {
                _doFullHDR = value;
                _settingsService.DoFullHDR = value;
                OnPropertyChanged();
            }
        }

        private bool _rememberSettings = false;

        public bool RememberSettings
        {
            get => _rememberSettings;
            set
            {
                _rememberSettings = value;
                _settingsService.RememberSettings = value;
                OnPropertyChanged();
            }
        }

        private bool _canStart;
        public bool CanStart
        {
            get => _canStart;
            set
            {
                if (_canStart != value)
                {
                    _canStart = value;
                    OnPropertyChanged();
                    (MergePhotosCommand as AsyncCommand)?.RaiseCanExecuteChanged();
                }
            }
        }
        
        private bool _rgbInterleaved = false;
        public bool RgbInterleaved
        {
            get => _rgbInterleaved;
            set
            {
                if (_rgbInterleaved != value)
                {
                    _rgbInterleaved = value;
                    _settingsService.RgbInterleaved = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _progressCounter;

        public string ProgressCounter
        {
            get => _progressCounter;
            set
            {
                _progressCounter = value;
                OnPropertyChanged();
            }
        }

        #endregion

        #region Commands

        public ICommand MergePhotosCommand { get; }

        public ICommand SelectSourceFileCommand { get; }
        public ICommand SelectDestinationPathCommand { get; }
        public ICommand OpenDestinationCommand { get; }
        public ICommand FileDropCommand { get; }

        #endregion

        #region Services

        private readonly ImageTransformService _imageTransformService;
        private readonly IDialogService _dialogService;
        private readonly IFileSystemService _fileSystemService;
        private readonly IRgbImageProcessingService _rgbImageProcessingService;
        private readonly ISettingsService _settingsService;

        #endregion

        public MainViewModel() { }
        public MainViewModel(ImageTransformService imageTransformService, IDialogService dialogService,
            IFileSystemService fileSystemService, IRgbImageProcessingService rgbImageProcessingService, ISettingsService settingsService)
        {
            // Assign Services
            _imageTransformService = imageTransformService;
            _dialogService = dialogService;
            _fileSystemService = fileSystemService;
            _rgbImageProcessingService = rgbImageProcessingService;
            _settingsService = settingsService;

            // Assign Commands
            MergePhotosCommand = new AsyncCommand(MergePhotos, () => CanStart);
            SelectSourceFileCommand = new RelayCommand(SelectSourceFile);
            SelectDestinationPathCommand = new RelayCommand(SelectDestinationPath);
            OpenDestinationCommand = new RelayCommand(OpenDestination);
            FileDropCommand = new RelayCommand(OnFileDrop);
            
            
            // Initializations
            _canStart = false;

            LoadSettings();
        }

        private void LoadSettings()
        {
            _settingsService.LoadSettings();

            SourcePath = _settingsService.SourcePath;
            DestinationPath = _settingsService.DestinationPath;

            RememberSettings = _settingsService.RememberSettings;

            DoHDR = _settingsService.DoHDR;
            DoRgbMerge = _settingsService.DoRGBMerge;

            //UpdateStartupCondition();
        }

        private string MakeOutputSubFolder(string source, string destination)
        {
            return System.IO.Path.Combine(destination, System.IO.Path.GetFileNameWithoutExtension(source));
        }
        
        private async Task MergePhotos()
        {
            if (string.IsNullOrWhiteSpace(SourcePath) || string.IsNullOrWhiteSpace(DestinationPath))
            {
                _dialogService.ShowMessage("Please the source and destination paths");
                return;
            }

            var outputSubFolder = MakeOutputSubFolder(SourcePath, DestinationPath); //System.IO.Path.Combine(DestinationPath, System.IO.Path.GetFileNameWithoutExtension(SourcePath));

            // Check if the input file exists
            if (!_fileSystemService.FileExists(SourcePath))
            {
                //Console.WriteLine($"The file {inputFilename} does not exist.");
                _dialogService.ShowMessage($"The file {SourcePath} does not exist.");

                return;
            }

            Stopwatch s = new Stopwatch();
            s.Start();

            IsWorking = true;

            // Check if the output directory exists, if not, create it
            _fileSystemService.CreateDirectory(outputSubFolder);

            ProgressCounter = string.Empty;

            if (DoRgbMerge)
            {
                var progress = new Progress<ProgressInfo>(ReportProgress);

                // Run Asynchronously to avoid locking the UI thread
                try
                {
                    var result = await Task.Run(() => _imageTransformService.TransformSav(SourcePath, outputSubFolder,
                        new ImportSavOptions()
                        {
                            // TODO: Set options
                            ImportLastSeen = false,
                            ImportDeleted = true,
                            ForceMagicCheck = false,
                            AverageType =
                                DoHDR ? DoFullHDR ? AverageTypes.FullBank : AverageTypes.Normal : AverageTypes.None,
                            ChannelOrder = RgbInterleaved ? ChannelOrder.Interleaved : ChannelOrder.Sequential,
                            AebStep = 2,
                            BanksToProcess = -1,
                            CartIsJp = false
                        }, progress));
                }
                catch (AggregateException e)
                {
                    foreach (var exceptions in e.InnerExceptions)
                    {
                        _dialogService.ShowError(e);
                        Debug.WriteLine(e.Message);
                    }
                }
                catch (Exception e)
                {
                    _dialogService.ShowError(e);
                    Debug.WriteLine(e);
                    
                }

                //await _imageTransformService.TransformSav(SourcePath, outputSubFolder);
            }

            // if (DoHDR)
            // {
            //     var progress = new Progress<ProgressInfo>(ReportProgress);
            //
            //     await Task.Run(() => _rgbImageProcessingService.ProcessImages(outputSubFolder, outputSubFolder, ChannelOrder.Sequential, progress));
            //     //_rgbImageProcessingService.ProcessImages(outputSubFolder, outputSubFolder, ChannelOrder.Sequential);
            // }
            
            IsWorking = false;
            s.Stop();
            
            ProgressCounter += $"\r\nTime: {s.Elapsed:g}";
            
            // TODO: Play a sound
            
            //_dialogService.ShowMessage("Done!");
            //UpdateStartupCondition();
        }

        private void ReportProgress(ProgressInfo value)
        {
            ProgressCounter = $"Bank: {value.CurrentBank}/{value.TotalBanks} Image: {value.CurrentImage}/{value.TotalImages} Name: {value.CurrentImageName}";
        }

        private void SelectSourceFile()
        {
            var dialogResult = _dialogService.OpenFileDialog();
            if (!string.IsNullOrWhiteSpace(dialogResult))
            {
                SourcePath = dialogResult;
            }

            //UpdateStartupCondition();
        }

        private void SelectDestinationPath()
        {
            var dialogResult = _dialogService.OpenFolderDialog();
            if (!string.IsNullOrWhiteSpace(dialogResult))
            {
                DestinationPath = dialogResult;
            }

            //UpdateStartupCondition();
        }

        private void OpenDestination()
        {
            if (string.IsNullOrWhiteSpace(DestinationPath))
            {
                return;
            }

            var subFolder = MakeOutputSubFolder(SourcePath, DestinationPath);
            
            if (string.IsNullOrWhiteSpace(subFolder))
            {
                Process.Start("explorer.exe", DestinationPath);
                return;
            }

            Process.Start("explorer.exe", subFolder);
        }

        private void UpdateStartupCondition()
        {
            CanStart = !string.IsNullOrWhiteSpace(SourcePath) && !string.IsNullOrWhiteSpace(DestinationPath) &&
                       !_isWorking;
        }

        private void OnFileDrop(object parameter)
        {
            if (parameter is string filePath)
            {
                SourcePath = filePath;
            }
        }
    }
}