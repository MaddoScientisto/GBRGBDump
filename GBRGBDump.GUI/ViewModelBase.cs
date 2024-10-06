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
