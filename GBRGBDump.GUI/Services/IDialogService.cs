using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace GBRGBDump.GUI.Services
{
    public interface IDialogService
    {
        void ShowMessage(string message, string caption);
        void ShowError(Exception ex);
        string? OpenFileDialog(string? filter = null);
        string? OpenFolderDialog(string? lastFolder = null);

        void ShowWindow<TWindow, TViewModel, TModel>()
            where TWindow : Window
            where TViewModel : ViewModelBase<TModel>;
        bool? ShowDialog<TWindow, TViewModel, TModel>()
            where TWindow : Window
            where TViewModel : ViewModelBase<TModel>;

        TModel? ShowDialog<TWindow, TViewModel, TModel>(TModel model)
            where TWindow : Window
            where TViewModel : ViewModelBase<TModel>;

        Task<(bool result, TModel model)> ShowDialogAsync<TWindow, TViewModel, TModel>(TModel model)
            where TWindow : Window
            where TViewModel : ViewModelBase<TModel>;
    }
}
