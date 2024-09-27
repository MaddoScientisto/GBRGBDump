using GBTools.Bootstrapper;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using GBRGBDump.GUI.Commands;
using GBRGBDump.GUI.Services;

namespace GBRGBDump.GUI
{
    public class MainViewModel : ViewModelBase
    {
        private string _sourcePath;

        public string SourcePath
        {
            get => _sourcePath;
            set
            {
                _sourcePath = value;
                OnPropertyChanged();
            }
        }

        public string _destinationPath;

        public string DestinationPath
        {
            get => _destinationPath;
            set
            {
                _destinationPath = value;
                OnPropertyChanged();
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
            }
        }

        public ICommand MergePhotosCommand { get; }

        public ICommand SelectSourceFileCommand { get; }
        public ICommand SelectDestinationPathCommand { get; }

        private ImageTransformService _imageTransformService;
        private IDialogService _dialogService;

        public MainViewModel(ImageTransformService imageTransformService, IDialogService dialogService)
        {
            _imageTransformService = imageTransformService;
            _dialogService = dialogService;

            MergePhotosCommand = new AsyncCommand(MergePhotos);

            SelectSourceFileCommand = new RelayCommand(SelectSourceFile);
            SelectDestinationPathCommand = new RelayCommand(SelectDestinationPath);
        }

        private async Task MergePhotos()
        {
            await Task.Delay(1000); // Simulate a delay
            Debug.WriteLine("Done");
        }

        private void SelectSourceFile()
        {
            var dialogResult = _dialogService.OpenFileDialog();
            if (!String.IsNullOrWhiteSpace(dialogResult))
            {
                SourcePath = dialogResult;
            }
        }

        private void SelectDestinationPath()
        {
            var dialogResult = _dialogService.OpenFolderDialog();
            if (!String.IsNullOrWhiteSpace(dialogResult))
            {
                DestinationPath = dialogResult;
            }
        }
    }
}