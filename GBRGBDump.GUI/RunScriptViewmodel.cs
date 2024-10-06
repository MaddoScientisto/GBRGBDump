using GBRGBDump.GUI.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

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

        #endregion

        public override void Initialize(RunScriptModel model)
        {
            base.Initialize(model);
        }

        public RunScriptViewmodel()
        {
            this.OkCommand = new RelayCommand(Ok);
            this.CancelCommand = new RelayCommand(Cancel);
        }

        public void Ok()
        {
            this.Model.Success = true;

            // TODO: Raise event to close dialog
        }

        public void Cancel()
        {
            this.Model.Success = false;
            
            // TODO: Raise event to close dialog
        }
    }
}
