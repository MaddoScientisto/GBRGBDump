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
    private int _currentImageCount;
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

    [ObservableProperty]
    private bool isMetadataActionRunning;

    public PicNRecRangeDialogViewModel(
        PicNRecDeviceInfo deviceInfo,
        IPicNRecImportService picNRecImportService,
        IBitmapFactory bitmapFactory)
    {
        _deviceInfo = deviceInfo;
        _picNRecImportService = picNRecImportService;
        _bitmapFactory = bitmapFactory;
        _currentImageCount = deviceInfo.ImageCount;
        endImageNumberText = Math.Max(0, deviceInfo.LastImageIndex).ToString(System.Globalization.CultureInfo.InvariantCulture);
        ConfirmCommand = new RelayCommand(Confirm);
        CancelCommand = new RelayCommand(Cancel);
        ExportToFilesCommand = new RelayCommand(ExportToFiles);
        ClearLastPhotoRecordCommand = new AsyncRelayCommand(ClearLastPhotoRecordAsync);
        PreviewImageNumber = Math.Max(0, deviceInfo.LastImageIndex);
    }

    public string DeviceSummary => _currentImageCount > 0
        ? $"Detected {_deviceInfo.PortName}. Last photo counter: {_currentImageCount} (last indexed photo {CurrentLastImageIndex})."
        : $"Detected {_deviceInfo.PortName}. The device reported no active photos, but slots can still be previewed in case deleted data remains.";

    public double MaxSupportedImageNumber => _deviceInfo.MaxSupportedImageIndex;

    public double LastImageMarker => Math.Max(0, CurrentLastImageIndex);

    public string LastImageMarkerText => _currentImageCount > 0
        ? $"Last photo counter marker: {CurrentLastImageIndex}. Slots up to {_deviceInfo.MaxSupportedImageIndex} can still be previewed."
        : $"No current last photo marker was reported. Slots up to {_deviceInfo.MaxSupportedImageIndex} can still be previewed.";

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

    public string PreviewPositionText => SelectedPreviewImageNumber == CurrentLastImageIndex
        ? "Selected slot is the last photo counter."
        : SelectedPreviewImageNumber > CurrentLastImageIndex
            ? "Selected slot is past the last photo counter and may be deleted or unused."
            : "Selected slot is before the last photo counter.";

    public bool CanRunMetadataAction => !IsMetadataActionRunning && !IsPreviewLoading;

    private int CurrentLastImageIndex => _currentImageCount - 1;

    private int SelectedPreviewImageNumber => Math.Clamp((int)Math.Round(PreviewImageNumber), 0, _deviceInfo.MaxSupportedImageIndex);

    public IRelayCommand ConfirmCommand { get; }

    public IRelayCommand CancelCommand { get; }

    public IRelayCommand ExportToFilesCommand { get; }

    public IAsyncRelayCommand ClearLastPhotoRecordCommand { get; }

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

    partial void OnIsPreviewLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanRunMetadataAction));
        ClearLastPhotoRecordCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsMetadataActionRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(CanRunMetadataAction));
        ClearLastPhotoRecordCommand.NotifyCanExecuteChanged();
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
            LoadedPhotoInfo photo = await _picNRecImportService.PreviewImageAsync(_deviceInfo.PortName, imageNumber, cancellationToken: cancellation.Token).ConfigureAwait(true);
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
        CloseRequested?.Invoke(new PicNRecDownloadRequest(_deviceInfo.PortName, startImageNumber, endImageNumber, PicNRecTransferTarget.ImportToGallery));
    }

    private void Cancel() => CloseRequested?.Invoke(null);

    private void ExportToFiles()
    {
        if (!TryCreateTransferRequest(PicNRecTransferTarget.ExportToFolder, out PicNRecDownloadRequest? request))
        {
            return;
        }

        CloseRequested?.Invoke(request);
    }

    private async Task ClearLastPhotoRecordAsync()
    {
        try
        {
            IsMetadataActionRunning = true;
            ErrorMessage = string.Empty;
            PreviewStatus = "Clearing the PicNRec last photo marker...";
            await _picNRecImportService.ClearLastImageMarkerAsync(_deviceInfo.PortName).ConfigureAwait(true);
            _currentImageCount = 0;
            PreviewStatus = "Cleared the last photo marker. Previewing deleted slots is still available.";
            OnPropertyChanged(nameof(DeviceSummary));
            OnPropertyChanged(nameof(LastImageMarker));
            OnPropertyChanged(nameof(LastImageMarkerText));
            OnPropertyChanged(nameof(PreviewPositionText));
        }
        catch (Exception error)
        {
            PreviewStatus = $"Clearing the last photo marker failed: {error.GetType().Name}: {error.Message}";
        }
        finally
        {
            IsMetadataActionRunning = false;
        }
    }

    private bool TryCreateTransferRequest(PicNRecTransferTarget target, out PicNRecDownloadRequest? request)
    {
        request = null;

        if (!int.TryParse(StartImageNumberText, out int startImageNumber)
            || !int.TryParse(EndImageNumberText, out int endImageNumber))
        {
            ErrorMessage = "Enter whole-number start and end image numbers.";
            return false;
        }

        if (startImageNumber < 0 || startImageNumber > _deviceInfo.MaxSupportedImageIndex)
        {
            ErrorMessage = $"Start must be between 0 and {_deviceInfo.MaxSupportedImageIndex}.";
            return false;
        }

        if (endImageNumber < startImageNumber || endImageNumber > _deviceInfo.MaxSupportedImageIndex)
        {
            ErrorMessage = $"End must be between {startImageNumber} and {_deviceInfo.MaxSupportedImageIndex}.";
            return false;
        }

        ErrorMessage = string.Empty;
        request = new PicNRecDownloadRequest(_deviceInfo.PortName, startImageNumber, endImageNumber, target);
        return true;
    }
}