using System.Linq;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using GBTools.ImageSharp.GameBoyCamera.Avalonia.ViewModels;

namespace GBTools.ImageSharp.GameBoyCamera.Avalonia.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _subscribedViewModel;

    public MainWindow()
    {
        InitializeComponent();
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _subscribedViewModel = DataContext as MainWindowViewModel;
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.OperationLogText))
        {
            Dispatcher.UIThread.Post(OperationLogScrollViewer.ScrollToEnd, DispatcherPriority.Background);
        }
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

    private void SelectionCheckBox_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel || sender is not CheckBox checkBox || checkBox.DataContext is not PhotoItemViewModel photo)
        {
            return;
        }

        viewModel.BeginSelectionInteraction(photo, e.KeyModifiers.HasFlag(KeyModifiers.Shift));
    }

    private void SelectionCheckBox_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel || sender is not CheckBox checkBox || checkBox.DataContext is not PhotoItemViewModel photo)
        {
            return;
        }

        viewModel.CompleteSelectionInteraction(photo);
    }

    private static bool TryGetLocalFilePath(DragEventArgs e, out string? path)
    {
        path = e.DataTransfer.TryGetFiles()?
            .Select(static file => file.Path.LocalPath)
            .FirstOrDefault(static localPath => !string.IsNullOrWhiteSpace(localPath));

        return !string.IsNullOrWhiteSpace(path);
    }
}