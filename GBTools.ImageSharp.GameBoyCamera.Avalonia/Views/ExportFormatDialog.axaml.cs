using Avalonia.Controls;
using GBTools.ImageSharp.GameBoyCamera.Avalonia.Models;
using GBTools.ImageSharp.GameBoyCamera.Avalonia.ViewModels;

namespace GBTools.ImageSharp.GameBoyCamera.Avalonia.Views;

public partial class ExportFormatDialog : Window
{
    public ExportFormatDialog()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is ExportFormatDialogViewModel viewModel)
        {
            viewModel.CloseRequested -= OnCloseRequested;
            viewModel.CloseRequested += OnCloseRequested;
        }
    }

    private void OnCloseRequested(ExportRequest? result)
    {
        Close(result);
    }
}