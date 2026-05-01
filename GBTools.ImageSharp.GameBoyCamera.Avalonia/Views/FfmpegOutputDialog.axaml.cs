using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;
using GBTools.ImageSharp.GameBoyCamera.Avalonia.ViewModels;

namespace GBTools.ImageSharp.GameBoyCamera.Avalonia.Views;

public partial class FfmpegOutputDialog : Window
{
    private FfmpegOutputDialogViewModel? _subscribedViewModel;

    public FfmpegOutputDialog()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (DataContext is FfmpegOutputDialogViewModel viewModel)
        {
            _subscribedViewModel = viewModel;
            _subscribedViewModel.PropertyChanged += OnViewModelPropertyChanged;
            viewModel.CloseRequested -= OnCloseRequested;
            viewModel.CloseRequested += OnCloseRequested;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FfmpegOutputDialogViewModel.ConsoleOutput))
        {
            Dispatcher.UIThread.Post(FfmpegOutputScrollViewer.ScrollToEnd, DispatcherPriority.Background);
        }
    }

    private void OnCloseRequested()
    {
        Close();
    }
}