using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using GBTools.ImageSharp.GameBoyCamera.Avalonia.ViewModels;

namespace GBTools.ImageSharp.GameBoyCamera.Avalonia.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = TryGetLocalFilePath(e, out _) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel || !TryGetLocalFilePath(e, out string? path) || string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        await viewModel.LoadFileAsync(path);
    }

    private void PageNumberTextBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.CommitPageNumberInput();
            e.Handled = true;
        }
    }

    private static bool TryGetLocalFilePath(DragEventArgs e, out string? path)
    {
        path = e.DataTransfer.TryGetFiles()?
            .Select(static file => file.Path.LocalPath)
            .FirstOrDefault(static localPath => !string.IsNullOrWhiteSpace(localPath));

        return !string.IsNullOrWhiteSpace(path);
    }
}