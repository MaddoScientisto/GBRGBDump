using GBRGBDump.GUI.Commands;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using GBRGBDump.GUI.Services;

namespace GBRGBDump.GUI
{
    public class RunScriptModel
    {
        public string Path { get; set; }
        public string Arguments { get; set; }

        public string RunLocation { get; set; }

        public bool Success { get; set; }
    }

    public class RunScriptViewmodel : ViewModelBase<RunScriptModel>
    {
        #region Binding Properties


        public string Path
        {
            get { return this.Model.Path; }
            set
            {
                this.Model.Path = value;
                OnPropertyChanged();
            }
        }

        public string Arguments
        {
            get { return this.Model.Arguments; }
            set
            {
                this.Model.Arguments = value;
                OnPropertyChanged();
            }
        }

        public string RunLocation
        {
            get { return this.Model.RunLocation; }
            set
            {
                this.Model.RunLocation = value;
                OnPropertyChanged();
            }
        }

        public ICommand OkCommand { get; }
        public ICommand CancelCommand { get; }
        
        public ICommand SelectFolderCommand { get; }
        public Action<string> SelectPathAction { get; }
        public Action<string> SelectRunLocationAction { get; }

        #endregion

        public override void Initialize(RunScriptModel model)
        {
            base.Initialize(model);
        }

        private readonly IDialogService _dialogService;
        
        public RunScriptViewmodel(IDialogService dialogService)
        {
            this.OkCommand = new RelayCommand(Ok);
            this.CancelCommand = new RelayCommand(Cancel);
            this.SelectFolderCommand = new RelayCommand(SelectFolder);
            
            SelectPathAction = (path) => Path = path;
            SelectRunLocationAction = (location) => RunLocation = location;
            
            _dialogService = dialogService;
        }

        private void SelectFolder(object parameter)
        {
            if (parameter is Action<string> setFolderPath)
            {
                var path = _dialogService.OpenFolderDialog();
                if (!string.IsNullOrWhiteSpace(path))
                {
                    setFolderPath(path);
                } 
            }
        }

        private void Ok()
        {
            this.Model.Success = true;

            // TODO: Raise event to close dialog
        }

        private void Cancel()
        {
            this.Model.Success = false;
            
            // TODO: Raise event to close dialog
        }
    }
}
