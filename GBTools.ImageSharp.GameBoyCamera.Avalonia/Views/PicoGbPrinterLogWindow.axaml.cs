using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;
using GBTools.ImageSharp.GameBoyCamera.Avalonia.ViewModels;

namespace GBTools.ImageSharp.GameBoyCamera.Avalonia.Views;

public partial class PicoGbPrinterLogWindow : Window
{
    private PicoGbPrinterLogWindowViewModel? _subscribedViewModel;

    public PicoGbPrinterLogWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _subscribedViewModel.CloseRequested -= OnCloseRequested;
        }

        if (DataContext is PicoGbPrinterLogWindowViewModel viewModel)
        {
            _subscribedViewModel = viewModel;
            _subscribedViewModel.PropertyChanged += OnViewModelPropertyChanged;
            _subscribedViewModel.CloseRequested += OnCloseRequested;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PicoGbPrinterLogWindowViewModel.ConsoleOutput))
        {
            Dispatcher.UIThread.Post(ConsoleScrollViewer.ScrollToEnd, DispatcherPriority.Background);
        }
    }

    private void OnCloseRequested()
    {
        Close();
    }
}