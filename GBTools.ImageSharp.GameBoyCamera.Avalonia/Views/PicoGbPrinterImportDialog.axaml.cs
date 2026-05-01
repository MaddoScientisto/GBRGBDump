using Avalonia.Controls;
using GBTools.ImageSharp.GameBoyCamera.Avalonia.Models;
using GBTools.ImageSharp.GameBoyCamera.Avalonia.ViewModels;

namespace GBTools.ImageSharp.GameBoyCamera.Avalonia.Views;

public partial class PicoGbPrinterImportDialog : Window
{
    public PicoGbPrinterImportDialog()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is PicoGbPrinterImportDialogViewModel viewModel)
        {
            viewModel.CloseRequested -= OnCloseRequested;
            viewModel.CloseRequested += OnCloseRequested;
        }
    }

    private void OnCloseRequested(PicoGbPrinterImportRequest? result)
    {
        Close(result);
    }
}