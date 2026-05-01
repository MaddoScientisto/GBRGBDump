using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GBTools.ImageSharp.GameBoyCamera.Avalonia.Models;

namespace GBTools.ImageSharp.GameBoyCamera.Avalonia.ViewModels;

public sealed partial class VideoExportDialogViewModel : ViewModelBase
{
    [ObservableProperty]
    private string outputPath = string.Empty;

    [ObservableProperty]
    private string magnificationText = "8";

    [ObservableProperty]
    private string frameRateText = "5";

    [ObservableProperty]
    private string errorMessage = string.Empty;

    public VideoExportDialogViewModel()
    {
        BrowseCommand = new RelayCommand(Browse);
        ConfirmCommand = new RelayCommand(Confirm);
        CancelCommand = new RelayCommand(Cancel);
    }

    public IRelayCommand BrowseCommand { get; }

    public IRelayCommand ConfirmCommand { get; }

    public IRelayCommand CancelCommand { get; }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public event Action? BrowseRequested;

    public event Action<VideoExportRequest?>? CloseRequested;

    partial void OnErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
    }

    private void Browse() => BrowseRequested?.Invoke();

    private void Confirm()
    {
        if (string.IsNullOrWhiteSpace(OutputPath))
        {
            ErrorMessage = "Choose an output MP4 path.";
            return;
        }

        if (!int.TryParse(MagnificationText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int magnification)
            || magnification < 1
            || magnification > 64)
        {
            ErrorMessage = "Enter a whole-number magnification between 1 and 64.";
            return;
        }

        if (!double.TryParse(FrameRateText, NumberStyles.Float, CultureInfo.InvariantCulture, out double frameRate)
        || frameRate <= 0)
        {
            ErrorMessage = "Enter a frame rate greater than 0.";
            return;
        }

        ErrorMessage = string.Empty;
        CloseRequested?.Invoke(new VideoExportRequest(OutputPath, magnification, frameRate));
    }

    private void Cancel() => CloseRequested?.Invoke(null);
}