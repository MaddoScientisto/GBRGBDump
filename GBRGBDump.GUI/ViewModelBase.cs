using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace GBRGBDump.GUI
{
    public abstract class ViewModelBase<TModel> : INotifyPropertyChanged
    {

        public TModel Model { get; set; }
        
        // Delegate to close the dialog and return a dialog result
        public Func<bool?, bool?> CloseDialog { get; set; }

        protected ViewModelBase()
        {
            // Initialize default behavior (can be overwritten)
            CloseDialog = _ => false;
        }

        public virtual void Initialize(TModel model)
        {
            Model = model;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
