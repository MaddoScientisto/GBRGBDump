using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Media.Imaging;
using GBTools.ImageSharp.GameBoyCamera.Avalonia.Models;
using GBTools.ImageSharp.GameBoyCamera.Avalonia.Services;

namespace GBTools.ImageSharp.GameBoyCamera.Avalonia.ViewModels;

public sealed partial class PicNRecRangeDialogViewModel : ViewModelBase, IDisposable
{
    private readonly PicNRecDeviceInfo _deviceInfo;
    private readonly IPicNRecImportService _picNRecImportService;
    private readonly IBitmapFactory _bitmapFactory;
    private CancellationTokenSource? _previewCancellation;
    private Bitmap? _previewBitmap;

    [ObservableProperty]
    private string startImageNumberText = "0";

    [ObservableProperty]
    private string endImageNumberText;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private double previewImageNumber;

    [ObservableProperty]
    private string previewStatus = "Move the slider to preview a PicNRec slot.";

    [ObservableProperty]
    private bool isPreviewLoading;

    public PicNRecRangeDialogViewModel(
        PicNRecDeviceInfo deviceInfo,
        IPicNRecImportService picNRecImportService,
        IBitmapFactory bitmapFactory)
    {
        _deviceInfo = deviceInfo;
        _picNRecImportService = picNRecImportService;
        _bitmapFactory = bitmapFactory;
        endImageNumberText = Math.Max(0, deviceInfo.LastImageIndex).ToString(System.Globalization.CultureInfo.InvariantCulture);
        ConfirmCommand = new RelayCommand(Confirm);
        CancelCommand = new RelayCommand(Cancel);
        PreviewImageNumber = Math.Max(0, deviceInfo.LastImageIndex);
    }

    public string DeviceSummary => $"Detected {_deviceInfo.PortName}. Last photo counter: {_deviceInfo.ImageCount} (last indexed photo {_deviceInfo.LastImageIndex}).";

    public double MaxSupportedImageNumber => _deviceInfo.MaxSupportedImageIndex;

    public double LastImageMarker => Math.Max(0, _deviceInfo.LastImageIndex);

    public string LastImageMarkerText => $"Last photo counter marker: {_deviceInfo.LastImageIndex}. Slots up to {_deviceInfo.MaxSupportedImageIndex} can still be previewed.";

    public Bitmap? PreviewBitmap
    {
        get => _previewBitmap;
        private set
        {
            if (ReferenceEquals(_previewBitmap, value))
            {
                return;
            }

            _previewBitmap?.Dispose();
            _previewBitmap = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasPreview));
            OnPropertyChanged(nameof(IsPreviewEmpty));
        }
    }

    public bool HasPreview => PreviewBitmap is not null;

    public bool IsPreviewEmpty => PreviewBitmap is null;

    public string PreviewImageNumberText => $"Preview slot {SelectedPreviewImageNumber}";

    public string PreviewPositionText => SelectedPreviewImageNumber == _deviceInfo.LastImageIndex
        ? "Selected slot is the last photo counter."
        : SelectedPreviewImageNumber > _deviceInfo.LastImageIndex
            ? "Selected slot is past the last photo counter and may be deleted or unused."
            : "Selected slot is before the last photo counter.";

    private int SelectedPreviewImageNumber => Math.Clamp((int)Math.Round(PreviewImageNumber), 0, _deviceInfo.MaxSupportedImageIndex);

    public IRelayCommand ConfirmCommand { get; }

    public IRelayCommand CancelCommand { get; }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public event Action<PicNRecDownloadRequest?>? CloseRequested;

    partial void OnErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
    }

    partial void OnPreviewImageNumberChanged(double value)
    {
        OnPropertyChanged(nameof(PreviewImageNumberText));
        OnPropertyChanged(nameof(PreviewPositionText));
        _ = PreviewSelectedImageAsync(SelectedPreviewImageNumber);
    }

    public void Dispose()
    {
        _previewCancellation?.Cancel();
        _previewCancellation?.Dispose();
        PreviewBitmap = null;
    }

    private async Task PreviewSelectedImageAsync(int imageNumber)
    {
        _previewCancellation?.Cancel();
        _previewCancellation?.Dispose();
        CancellationTokenSource cancellation = new();
        _previewCancellation = cancellation;

        try
        {
            IsPreviewLoading = true;
            PreviewStatus = $"Previewing slot {imageNumber}...";
            await Task.Delay(350, cancellation.Token).ConfigureAwait(true);
            LoadedPhotoInfo photo = await _picNRecImportService.PreviewImageAsync(_deviceInfo.PortName, imageNumber, cancellation.Token).ConfigureAwait(true);
            if (ReferenceEquals(_previewCancellation, cancellation))
            {
                PreviewBitmap = _bitmapFactory.CreatePreviewBitmap(photo.Photo);
                PreviewStatus = $"Preview loaded for slot {imageNumber}.";
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception error)
        {
            if (ReferenceEquals(_previewCancellation, cancellation))
            {
                PreviewStatus = $"Preview failed for slot {imageNumber}: {error.GetType().Name}: {error.Message}";
                PreviewBitmap = null;
            }
        }
        finally
        {
            if (ReferenceEquals(_previewCancellation, cancellation))
            {
                IsPreviewLoading = false;
            }
        }
    }

    private void Confirm()
    {
        if (!int.TryParse(StartImageNumberText, out int startImageNumber)
            || !int.TryParse(EndImageNumberText, out int endImageNumber))
        {
            ErrorMessage = "Enter whole-number start and end image numbers.";
            return;
        }

        if (startImageNumber < 0 || startImageNumber > _deviceInfo.MaxSupportedImageIndex)
        {
            ErrorMessage = $"Start must be between 0 and {_deviceInfo.MaxSupportedImageIndex}.";
            return;
        }

        if (endImageNumber < startImageNumber || endImageNumber > _deviceInfo.MaxSupportedImageIndex)
        {
            ErrorMessage = $"End must be between {startImageNumber} and {_deviceInfo.MaxSupportedImageIndex}.";
            return;
        }

        ErrorMessage = string.Empty;
        CloseRequested?.Invoke(new PicNRecDownloadRequest(_deviceInfo.PortName, startImageNumber, endImageNumber));
    }

    private void Cancel() => CloseRequested?.Invoke(null);
}