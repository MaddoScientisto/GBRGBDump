using Avalonia.Controls;
using GBTools.ImageSharp.GameBoyCamera.Avalonia.ViewModels;

namespace GBTools.ImageSharp.GameBoyCamera.Avalonia.Views;

public partial class PngMagnificationDialog : Window
{
    public PngMagnificationDialog()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is PngMagnificationDialogViewModel viewModel)
        {
            viewModel.CloseRequested -= OnCloseRequested;
            viewModel.CloseRequested += OnCloseRequested;
        }
    }

    private void OnCloseRequested(int? result)
    {
        Close(result);
    }
}