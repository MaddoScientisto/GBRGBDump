using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GBTools.ImageSharp.GameBoyCamera.Avalonia.Models;
using GBTools.ImageSharp.GameBoyCamera.Avalonia.Services;
using GBTools.ImageSharp.GameBoyCamera.Composition;
using GBTools.ImageSharp.GameBoyCamera.Model;
using Microsoft.Extensions.Logging;

namespace GBTools.ImageSharp.GameBoyCamera.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private const string NameAscendingLabel = "Name ascending";
    private const string NameDescendingLabel = "Name descending";
    private const string DateAscendingLabel = "Date ascending";
    private const string DateDescendingLabel = "Date descending";

    private static readonly IReadOnlyList<string> SortOptions =
    [
        NameAscendingLabel,
        NameDescendingLabel,
        DateAscendingLabel,
        DateDescendingLabel,
    ];

    private readonly IAlbumLoadService _albumLoadService;
    private readonly IBitmapFactory _bitmapFactory;
    private readonly IDialogService _dialogService;
    private readonly IImageExportService _imageExportService;
    private readonly IPicNRecImportService _picNRecImportService;
    private readonly IVideoExportService _videoExportService;
    private readonly ILogger<MainWindowViewModel> _logger;
    private List<PhotoItemViewModel> _sortedPhotos = [];
    private int _currentPageIndex;
    private PhotoItemViewModel? _selectionAnchorPhoto;
    private PhotoItemViewModel? _pendingSelectionPhoto;
    private bool _pendingShiftRangeSelection;
    private bool _isUpdatingSelectionInBulk;
    private CancellationTokenSource? _operationCancellation;
    private DateTime? _picNRecDownloadStartedAt;

    [ObservableProperty]
    private PhotoItemViewModel? selectedPhoto;

    [ObservableProperty]
    private string sourceSummary = "No file loaded.";

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool isLoadingPhotos;

    [ObservableProperty]
    private string operationProgressText = string.Empty;

    [ObservableProperty]
    private string operationLogText = string.Empty;

    [ObservableProperty]
    private double operationProgressValue;

    [ObservableProperty]
    private double operationProgressMaximum = 1d;

    [ObservableProperty]
    private string pageNumberText = "1";

    [ObservableProperty]
    private int pageSize = 30;

    [ObservableProperty]
    private string selectedOrdering = NameAscendingLabel;

    public MainWindowViewModel(
        IAlbumLoadService albumLoadService,
        IBitmapFactory bitmapFactory,
        IDialogService dialogService,
        IImageExportService imageExportService,
        IPicNRecImportService picNRecImportService,
        IVideoExportService videoExportService,
        ILogger<MainWindowViewModel> logger)
    {
        _albumLoadService = albumLoadService;
        _bitmapFactory = bitmapFactory;
        _dialogService = dialogService;
        _imageExportService = imageExportService;
        _picNRecImportService = picNRecImportService;
        _videoExportService = videoExportService;
        _logger = logger;

        Photos = [];
        VisiblePhotos = [];
        LoadImagesCommand = new AsyncRelayCommand(LoadImagesAsync, CanRunCommands);
        DownloadPicNRecCommand = new AsyncRelayCommand(DownloadPicNRecAsync, CanRunCommands);
        CancelOperationCommand = new RelayCommand(CancelOperation, () => CanCancelOperation);
        ExportSelectedCommand = new AsyncRelayCommand(ExportSelectedAsync, CanExportSelected);
        ExportSelectedVideoCommand = new AsyncRelayCommand(ExportSelectedVideoAsync, CanExportSelected);
        ComposeRgbCommand = new AsyncRelayCommand(ComposeRgbAsync, CanComposeRgb);
        ComposeAverageCommand = new AsyncRelayCommand(ComposeAverageAsync, CanComposeAverage);
        ComposeRgbAverageCommand = new AsyncRelayCommand(ComposeRgbAverageAsync, CanComposeSmartAverage);
        ToggleSelectAllCommand = new RelayCommand(ToggleSelectAll, () => HasPhotos);
        FirstPageCommand = new RelayCommand(MoveToFirstPage, () => CanMoveToFirstPage);
        PreviousPageCommand = new RelayCommand(MoveToPreviousPage, () => CanMoveToPreviousPage);
        NextPageCommand = new RelayCommand(MoveToNextPage, () => CanMoveToNextPage);
        LastPageCommand = new RelayCommand(MoveToLastPage, () => CanMoveToLastPage);
        PreviousSelectedPhotoCommand = new RelayCommand(MoveToPreviousSelectedPhoto, () => CanMoveToPreviousSelectedPhoto);
        NextSelectedPhotoCommand = new RelayCommand(MoveToNextSelectedPhoto, () => CanMoveToNextSelectedPhoto);
    }

    public ObservableCollection<PhotoItemViewModel> Photos { get; }

    public ObservableCollection<PhotoItemViewModel> VisiblePhotos { get; }

    public IAsyncRelayCommand LoadImagesCommand { get; }

    public IAsyncRelayCommand DownloadPicNRecCommand { get; }

    public IRelayCommand CancelOperationCommand { get; }

    public IAsyncRelayCommand ExportSelectedCommand { get; }

    public IAsyncRelayCommand ExportSelectedVideoCommand { get; }

    public IAsyncRelayCommand ComposeRgbCommand { get; }

    public IAsyncRelayCommand ComposeAverageCommand { get; }

    public IAsyncRelayCommand ComposeRgbAverageCommand { get; }

    public IRelayCommand ToggleSelectAllCommand { get; }

    public IRelayCommand FirstPageCommand { get; }

    public IRelayCommand PreviousPageCommand { get; }

    public IRelayCommand NextPageCommand { get; }

    public IRelayCommand LastPageCommand { get; }

    public IRelayCommand PreviousSelectedPhotoCommand { get; }

    public IRelayCommand NextSelectedPhotoCommand { get; }

    public IReadOnlyList<string> OrderingOptions => SortOptions;

    public bool HasPhotos => Photos.Count > 0;

    public bool IsEmptyStateVisible => !IsLoadingPhotos && !HasPhotos;

    public bool HasOperationProgress => IsLoadingPhotos && !string.IsNullOrWhiteSpace(OperationProgressText);

    public bool HasOperationLog => !string.IsNullOrWhiteSpace(OperationLogText);

    public bool CanCancelOperation => _operationCancellation is not null && !_operationCancellation.IsCancellationRequested;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasSelection => SelectedPhoto is not null;

    public bool IsSelectionEmpty => SelectedPhoto is null;

    public bool HasSelectedPhotos => Photos.Any(static photo => photo.IsSelected);

    public string ToggleSelectAllButtonText => HasSelectedPhotos ? "Deselect All" : "Select All";

    public string SelectedPhotoTitle => SelectedPhoto?.Title ?? "Nothing selected.";

    public int CurrentPageNumber => HasPhotos ? _currentPageIndex + 1 : 1;

    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Photos.Count / (double)EffectivePageSize));

    public string TotalPagesText => TotalPages.ToString(CultureInfo.InvariantCulture);

    public bool CanMoveToFirstPage => HasPhotos && _currentPageIndex > 0;

    public bool CanMoveToPreviousPage => HasPhotos && _currentPageIndex > 0;

    public bool CanMoveToNextPage => HasPhotos && _currentPageIndex < TotalPages - 1;

    public bool CanMoveToLastPage => HasPhotos && _currentPageIndex < TotalPages - 1;

    public bool CanMoveToPreviousSelectedPhoto => SelectedPhoto is not null && GetSelectedPhotoIndex() > 0;

    public bool CanMoveToNextSelectedPhoto => SelectedPhoto is not null && GetSelectedPhotoIndex() >= 0 && GetSelectedPhotoIndex() < _sortedPhotos.Count - 1;

    public Task LoadFileAsync(string path) => LoadFromPathAsync(path);

    public void Dispose()
    {
        ClearPhotos();
    }

    public void CommitPageNumberInput()
    {
        if (!HasPhotos)
        {
            PageNumberText = "1";
            return;
        }

        if (!int.TryParse(PageNumberText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedPageNumber))
        {
            parsedPageNumber = CurrentPageNumber;
        }

        if (parsedPageNumber < 1)
        {
            parsedPageNumber = 1;
        }

        MoveToPage(parsedPageNumber - 1);
    }

    partial void OnSelectedPhotoChanged(PhotoItemViewModel? oldValue, PhotoItemViewModel? newValue)
    {
        oldValue?.ReleasePreviewBitmap();
        newValue?.EnsurePreviewBitmap();
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(IsSelectionEmpty));
        OnPropertyChanged(nameof(SelectedPhotoTitle));
        PreviousSelectedPhotoCommand.NotifyCanExecuteChanged();
        NextSelectedPhotoCommand.NotifyCanExecuteChanged();
    }

    partial void OnErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
    }

    partial void OnIsBusyChanged(bool value)
    {
        LoadImagesCommand.NotifyCanExecuteChanged();
        DownloadPicNRecCommand.NotifyCanExecuteChanged();
        CancelOperationCommand.NotifyCanExecuteChanged();
        ExportSelectedCommand.NotifyCanExecuteChanged();
        ExportSelectedVideoCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsLoadingPhotosChanged(bool value)
    {
        OnPropertyChanged(nameof(HasOperationProgress));
    }

    partial void OnOperationProgressTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasOperationProgress));
    }

    partial void OnOperationLogTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasOperationLog));
    }

    partial void OnPageSizeChanged(int value)
    {
        if (value < 1)
        {
            if (PageSize != 1)
            {
                PageSize = 1;
            }

            return;
        }

        ApplyOrderingAndPagination();
    }

    partial void OnSelectedOrderingChanged(string value)
    {
        ApplyOrderingAndPagination();
    }

    private bool CanRunCommands() => !IsBusy;

    private bool CanExportSelected() => !IsBusy && Photos.Any(static photo => photo.IsSelected);

    private bool CanComposeRgb() => !IsBusy && GetSelectedMonochromePhotos().Count >= 3;

    private bool CanComposeAverage() => !IsBusy && GetSelectedPhotos().Count >= 3;

    private bool CanComposeSmartAverage() => !IsBusy && GetSelectedMonochromePhotos().Count >= 3;

    private async Task LoadImagesAsync()
    {
        string? path = await _dialogService.OpenSupportedImageAsync().ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        await LoadFromPathAsync(path).ConfigureAwait(true);
    }

    private async Task ExportSelectedAsync()
    {
        IReadOnlyList<PhotoItemViewModel> selectedPhotos = Photos.Where(static photo => photo.IsSelected).ToArray();
        if (selectedPhotos.Count == 0)
        {
            return;
        }

        ExportRequest? exportRequest = await _dialogService.SelectExportRequestAsync().ConfigureAwait(true);
        if (exportRequest is null)
        {
            return;
        }

        bool exportToSingleFile = selectedPhotos.Count == 1
            || (exportRequest.Format.SupportsSingleFileAlbumExport() && exportRequest.ExportSelectedToSingleFile);

        string? destination = exportToSingleFile
            ? await _dialogService.SaveExportFileAsync(exportRequest.Format, selectedPhotos[0].SafeFileStem).ConfigureAwait(true)
            : await _dialogService.PickExportFolderAsync().ConfigureAwait(true);

        if (string.IsNullOrWhiteSpace(destination))
        {
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;

            IReadOnlyList<PhotoExportRequest> exportRequests = selectedPhotos
                .Select(static photo => new PhotoExportRequest(photo.SafeFileStem, photo.Title, photo.Created, photo.Photo))
                .ToArray();

            await _imageExportService.ExportAsync(exportRequests, exportRequest, destination, !exportToSingleFile).ConfigureAwait(true);
            SourceSummary = exportRequest.Format == ExportFormat.Png
                ? $"Exported {selectedPhotos.Count} image(s) as {exportRequest.Format.GetDisplayName()} at {exportRequest.PngMagnification}x magnification."
                : exportRequest.Format == ExportFormat.GbPrinterWebJson && exportToSingleFile && selectedPhotos.Count > 1
                    ? $"Exported {selectedPhotos.Count} image(s) to one {exportRequest.Format.GetDisplayName()} file."
                : $"Exported {selectedPhotos.Count} image(s) as {exportRequest.Format.GetDisplayName()}.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export selected images.");
            ErrorMessage = FormatUserVisibleException(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExportSelectedVideoAsync()
    {
        IReadOnlyList<PhotoItemViewModel> selectedPhotos = Photos.Where(static photo => photo.IsSelected).ToArray();
        if (selectedPhotos.Count == 0)
        {
            return;
        }

        VideoExportRequest? request = await _dialogService.SelectVideoExportRequestAsync(selectedPhotos[0].SafeFileStem).ConfigureAwait(true);
        if (request is null)
        {
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;

            IReadOnlyList<PhotoExportRequest> exportRequests = selectedPhotos
                .Select(static photo => new PhotoExportRequest(photo.SafeFileStem, photo.Title, photo.Created, photo.Photo))
                .ToArray();

            await _dialogService.ShowFfmpegOutputAsync(
                request.OutputPath,
                (consoleOutput, cancellationToken) => _videoExportService.ExportAsync(exportRequests, request, consoleOutput, cancellationToken)).ConfigureAwait(true);
            SourceSummary = $"Exported {selectedPhotos.Count} selected image(s) to video at {request.Magnification}x magnification.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export selected images as video.");
            ErrorMessage = FormatUserVisibleException(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DownloadPicNRecAsync()
    {
        PicNRecDownloadRequest? request = null;
        List<LoadedPhotoInfo> downloadedPhotos = [];

        try
        {
            CancellationTokenSource operationCancellation = BeginCancellableOperation();
            IsBusy = true;
            IsLoadingPhotos = true;
            ErrorMessage = string.Empty;
            OperationLogText = string.Empty;
            OperationProgressText = "Detecting PicNRec device...";
            OperationProgressValue = 0;
            OperationProgressMaximum = 1;

            Progress<PicNRecDiscoveryProgress> discoveryProgress = new(UpdatePicNRecDiscoveryProgress);
            PicNRecDeviceInfo deviceInfo = await _picNRecImportService.DetectAsync(discoveryProgress, operationCancellation.Token).ConfigureAwait(true);
            SourceSummary = $"Detected PicNRec on {deviceInfo.PortName}. Available images: {deviceInfo.ImageCount}.";
            IsLoadingPhotos = false;
            IsBusy = false;
            OperationProgressText = string.Empty;
            EndCancellableOperation();

            request = await _dialogService.SelectPicNRecDownloadRequestAsync(deviceInfo).ConfigureAwait(true);
            if (request is null)
            {
                return;
            }

            operationCancellation = BeginCancellableOperation();
            IsBusy = true;
            IsLoadingPhotos = true;
            OperationLogText = string.Empty;
            OperationProgressMaximum = request.ImageCount;
            OperationProgressValue = 0;
            _picNRecDownloadStartedAt = DateTime.UtcNow;
            OperationProgressText = $"Preparing to download images {request.StartImageNumber} to {request.EndImageNumber}.";

            Progress<PicNRecDownloadProgress> progress = new(progress => UpdatePicNRecProgress(progress, downloadedPhotos));
            LoadedAlbumResult loadedAlbum = await _picNRecImportService.DownloadImagesAsync(request, progress, operationCancellation.Token).ConfigureAwait(true);
            ReplacePhotos(loadedAlbum.Photos);
            SourceSummary = $"Downloaded {Photos.Count} image(s) from PicNRec images {request.StartImageNumber} to {request.EndImageNumber}.";
        }
        catch (OperationCanceledException)
        {
            if (downloadedPhotos.Count > 0)
            {
                ReplacePhotos(downloadedPhotos);
                SourceSummary = $"PicNRec operation canceled. Showing {downloadedPhotos.Count} downloaded image(s).";
            }
            else
            {
                SourceSummary = request is null ? "PicNRec detection canceled." : "PicNRec download canceled before any images were downloaded.";
            }

            ErrorMessage = string.Empty;
        }
        catch (Exception ex)
        {
            if (request is not null)
            {
                ClearPhotos();
            }

            _logger.LogError(ex, "Failed to download images from PicNRec.");
            ErrorMessage = FormatUserVisibleException(ex);
            SourceSummary = request is null ? SourceSummary : "No file loaded.";
        }
        finally
        {
            IsLoadingPhotos = false;
            IsBusy = false;
            OperationProgressText = string.Empty;
            EndCancellableOperation();
        }
    }

    private void UpdatePicNRecDiscoveryProgress(PicNRecDiscoveryProgress progress)
    {
        OperationProgressText = progress.Message;
        AppendOperationLog(progress.Message);

        if (progress.Succeeded == true)
        {
            OperationProgressValue = OperationProgressMaximum;
        }
    }

    private void UpdatePicNRecProgress(PicNRecDownloadProgress progress, List<LoadedPhotoInfo> downloadedPhotos)
    {
        if (progress.ClearsDownloadedPhotos)
        {
            downloadedPhotos.Clear();
        }

        if (progress.DownloadedPhoto is not null)
        {
            downloadedPhotos.Add(progress.DownloadedPhoto);
        }

        OperationProgressMaximum = Math.Max(1, progress.TotalImageCount);
        OperationProgressValue = Math.Clamp(progress.CompletedImageCount, 0, progress.TotalImageCount);
        string etaText = FormatEstimatedTimeRemaining(progress.CompletedImageCount, progress.TotalImageCount);
        OperationProgressText = string.IsNullOrWhiteSpace(etaText)
            ? $"{progress.Message} ({progress.CompletedImageCount}/{progress.TotalImageCount})"
            : $"{progress.Message} ({progress.CompletedImageCount}/{progress.TotalImageCount}, ETA {etaText})";
        AppendOperationLog(OperationProgressText);
    }

    private string FormatEstimatedTimeRemaining(int completedImageCount, int totalImageCount)
    {
        if (_picNRecDownloadStartedAt is not DateTime startedAt
            || completedImageCount <= 0
            || completedImageCount >= totalImageCount)
        {
            return string.Empty;
        }

        TimeSpan elapsed = DateTime.UtcNow - startedAt;
        double secondsPerImage = elapsed.TotalSeconds / completedImageCount;
        double remainingSeconds = Math.Max(0, (totalImageCount - completedImageCount) * secondsPerImage);
        TimeSpan remaining = TimeSpan.FromSeconds(remainingSeconds);
        return remaining.TotalHours >= 1
            ? remaining.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
            : remaining.ToString(@"m\:ss", CultureInfo.InvariantCulture);
    }

    private CancellationTokenSource BeginCancellableOperation()
    {
        EndCancellableOperation();
        _operationCancellation = new CancellationTokenSource();
        OnPropertyChanged(nameof(CanCancelOperation));
        CancelOperationCommand.NotifyCanExecuteChanged();
        return _operationCancellation;
    }

    private void EndCancellableOperation()
    {
        _operationCancellation?.Dispose();
        _operationCancellation = null;
        OnPropertyChanged(nameof(CanCancelOperation));
        CancelOperationCommand.NotifyCanExecuteChanged();
    }

    private void CancelOperation()
    {
        OperationProgressText = "Canceling PicNRec operation...";
        _operationCancellation?.Cancel();
        OnPropertyChanged(nameof(CanCancelOperation));
        CancelOperationCommand.NotifyCanExecuteChanged();
    }

    private void AppendOperationLog(string message)
    {
        OperationLogText = string.IsNullOrWhiteSpace(OperationLogText)
            ? message
            : OperationLogText + Environment.NewLine + message;
    }

    private async Task ComposeRgbAsync()
    {
        IReadOnlyList<PhotoItemViewModel> selectedPhotos = GetSelectedMonochromePhotos();
        if (selectedPhotos.Count == 0)
        {
            return;
        }

        RgbCompositionRequest? request = await _dialogService.SelectRgbCompositionRequestAsync(
            selectedPhotos.Select(static photo => photo.Photo).ToArray()).ConfigureAwait(true);
        if (request is null)
        {
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;

            IReadOnlyList<GbcPhoto> generatedPhotos = GameBoyCameraCompositionService.CreateRgbPhotos(
                selectedPhotos.Select(static photo => photo.Photo).ToArray(),
                new GameBoyCameraRgbCompositionOptions(request.ChannelOrder));

            AppendGeneratedPhotos(generatedPhotos, "RGB Composite");
            SourceSummary = $"Added {generatedPhotos.Count} RGB composite image(s).";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create RGB composites.");
            ErrorMessage = FormatUserVisibleException(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ComposeAverageAsync()
    {
        IReadOnlyList<PhotoItemViewModel> selectedPhotos = GetSelectedPhotos();
        if (selectedPhotos.Count == 0)
        {
            return;
        }

        DirectAverageCompositionRequest? request = await _dialogService.SelectAverageCompositionRequestAsync(
            selectedPhotos.Select(static photo => photo.Photo).ToArray()).ConfigureAwait(true);
        if (request is null)
        {
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;

            GbcPhoto averagePhoto = GameBoyCameraCompositionService.CreateDirectAveragePhoto(
                selectedPhotos.Select(static photo => photo.Photo).ToArray());

            AppendGeneratedPhotos([averagePhoto], "Average Composite");
            SourceSummary = $"Added 1 average image from {selectedPhotos.Count} selected photo(s).";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create average composites.");
            ErrorMessage = FormatUserVisibleException(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ComposeRgbAverageAsync()
    {
        IReadOnlyList<PhotoItemViewModel> selectedPhotos = GetSelectedMonochromePhotos();
        if (selectedPhotos.Count == 0)
        {
            return;
        }

        SmartAverageCompositionRequest? request = await _dialogService.SelectRgbAverageCompositionRequestAsync(
            selectedPhotos.Select(static photo => photo.Photo).ToArray()).ConfigureAwait(true);
        if (request is null)
        {
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;

            GameBoyCameraAverageCompositionResult result = GameBoyCameraCompositionService.CreateAveragePhotos(
                selectedPhotos.Select(static photo => photo.Photo).ToArray(),
                new GameBoyCameraAverageCompositionOptions(request.ChannelOrder, request.Mode));

            IReadOnlyList<GbcPhoto> finalPhotos = request.Mode == GameBoyCameraAverageCompositionMode.FullBank
                ? result.AveragePhotos.TakeLast(1).ToArray()
                : result.AveragePhotos;

            AppendGeneratedPhotos(finalPhotos, "Average Composite");
            string groupSizes = string.Join(" + ", result.SourceGroupSizes.Select(static size => size.ToString(CultureInfo.InvariantCulture)));
            SourceSummary = $"Added {finalPhotos.Count} RGB+average result image(s) from groups {groupSizes}.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create RGB+average composites.");
            ErrorMessage = FormatUserVisibleException(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadFromPathAsync(string path)
    {
        try
        {
            IsBusy = true;
            IsLoadingPhotos = true;
            ErrorMessage = string.Empty;

            LoadedAlbumResult loadedAlbum = await _albumLoadService.LoadAsync(path).ConfigureAwait(true);

            ReplacePhotos(loadedAlbum.Photos);
            SourceSummary = $"Loaded {Photos.Count} image(s) from {Path.GetFileName(path)} as {loadedAlbum.SourceKind}.";
        }
        catch (Exception ex)
        {
            ClearPhotos();
            _logger.LogError(ex, "Failed to load images from {Path}", path);
            ErrorMessage = FormatUserVisibleException(ex);
            SourceSummary = "No file loaded.";
        }
        finally
        {
            IsLoadingPhotos = false;
            IsBusy = false;
        }
    }

    private void SelectPhoto(PhotoItemViewModel photo)
    {
        SelectedPhoto = photo;
    }

    private void OnPhotoPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PhotoItemViewModel.IsSelected))
        {
            if (!_isUpdatingSelectionInBulk)
            {
                UpdateSelectionState();
            }
        }
    }

    public void BeginSelectionInteraction(PhotoItemViewModel photo, bool shiftPressed)
    {
        ArgumentNullException.ThrowIfNull(photo);

        _pendingSelectionPhoto = photo;
        _pendingShiftRangeSelection = shiftPressed && _selectionAnchorPhoto is not null && !ReferenceEquals(_selectionAnchorPhoto, photo);
    }

    public void CompleteSelectionInteraction(PhotoItemViewModel photo)
    {
        ArgumentNullException.ThrowIfNull(photo);

        bool applyRangeSelection = _pendingShiftRangeSelection && ReferenceEquals(_pendingSelectionPhoto, photo) && _selectionAnchorPhoto is not null;
        _pendingSelectionPhoto = null;
        _pendingShiftRangeSelection = false;

        if (applyRangeSelection)
        {
            ApplySelectionRange(_selectionAnchorPhoto!, photo, photo.IsSelected);
            _selectionAnchorPhoto = photo;
            UpdateSelectionState();
            return;
        }

        _selectionAnchorPhoto = photo;
        UpdateSelectionState();
    }

    private void ClearPhotos()
    {
        foreach (PhotoItemViewModel photo in Photos)
        {
            photo.PropertyChanged -= OnPhotoPropertyChanged;
            photo.Dispose();
        }

        Photos.Clear();
        VisiblePhotos.Clear();
        _sortedPhotos.Clear();
        _currentPageIndex = 0;
        _selectionAnchorPhoto = null;
        _pendingSelectionPhoto = null;
        _pendingShiftRangeSelection = false;
        PageNumberText = "1";
        SelectedPhoto = null;
        OnPropertyChanged(nameof(HasPhotos));
        OnPropertyChanged(nameof(IsEmptyStateVisible));
        OnPropertyChanged(nameof(HasSelectedPhotos));
        OnPropertyChanged(nameof(ToggleSelectAllButtonText));
        UpdateNavigationState();
    }

    private void ReplacePhotos(IReadOnlyList<LoadedPhotoInfo> photos)
    {
        ClearPhotos();

        foreach (LoadedPhotoInfo photo in photos)
        {
            PhotoItemViewModel viewModel = new(
                photo.Title,
                photo.Created,
                photo.Photo,
                _bitmapFactory.CreateThumbnailBitmap(photo.Photo),
                () => _bitmapFactory.CreatePreviewBitmap(photo.Photo),
                photo.MetadataEntries,
                SelectPhoto);
            viewModel.PropertyChanged += OnPhotoPropertyChanged;
            Photos.Add(viewModel);
        }

        ApplyOrderingAndPagination(preserveSelection: false);
        OnPropertyChanged(nameof(HasPhotos));
        OnPropertyChanged(nameof(IsEmptyStateVisible));
        ExportSelectedCommand.NotifyCanExecuteChanged();
        ExportSelectedVideoCommand.NotifyCanExecuteChanged();
    }

    private IReadOnlyList<PhotoItemViewModel> GetSelectedMonochromePhotos()
        => Photos.Where(static photo => photo.IsSelected && photo.Photo.RgbnData is null && photo.Photo.AverageData is null).ToArray();

    private IReadOnlyList<PhotoItemViewModel> GetSelectedPhotos()
        => Photos.Where(static photo => photo.IsSelected).ToArray();

    private void AppendGeneratedPhotos(IReadOnlyList<GbcPhoto> generatedPhotos, string titlePrefix)
    {
        if (generatedPhotos.Count == 0)
        {
            return;
        }

        string created = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss:fff", CultureInfo.InvariantCulture);
        int startIndex = Photos.Count;
        List<PhotoItemViewModel> addedPhotos = [];

        for (int index = 0; index < generatedPhotos.Count; index++)
        {
            GbcPhoto photo = generatedPhotos[index];
            PhotoItemViewModel viewModel = new(
                $"{titlePrefix} {index + 1:D2}",
                created,
                photo,
                _bitmapFactory.CreateThumbnailBitmap(photo),
                () => _bitmapFactory.CreatePreviewBitmap(photo),
                PhotoMetadataEntryBuilder.Build(photo, null, startIndex + index, "Composed"),
                SelectPhoto);

            viewModel.PropertyChanged += OnPhotoPropertyChanged;
            Photos.Add(viewModel);
            addedPhotos.Add(viewModel);
        }

        ApplyOrderingAndPagination(preserveSelection: false);
        SelectedPhoto = addedPhotos.FirstOrDefault();
        OnPropertyChanged(nameof(HasPhotos));
        OnPropertyChanged(nameof(IsEmptyStateVisible));
        UpdateNavigationState();
    }

    private void ApplyOrderingAndPagination(bool preserveSelection = true)
    {
        PhotoItemViewModel? previousSelection = preserveSelection ? SelectedPhoto : null;
        _sortedPhotos = OrderPhotos(Photos).ToList();

        if (previousSelection is not null)
        {
            int selectedIndex = _sortedPhotos.IndexOf(previousSelection);
            if (selectedIndex >= 0)
            {
                _currentPageIndex = GetPageIndexForIndex(selectedIndex);
            }
        }

        _currentPageIndex = Math.Clamp(_currentPageIndex, 0, Math.Max(0, TotalPages - 1));
        RefreshVisiblePhotos();

        if (previousSelection is null || !_sortedPhotos.Contains(previousSelection))
        {
            SelectedPhoto = _sortedPhotos.FirstOrDefault();
        }

        UpdateNavigationState();
    }

    private IEnumerable<PhotoItemViewModel> OrderPhotos(IEnumerable<PhotoItemViewModel> photos)
    {
        return SelectedOrdering switch
        {
            NameDescendingLabel => photos.OrderByDescending(static photo => photo.Title, StringComparer.OrdinalIgnoreCase),
            DateAscendingLabel => photos.OrderBy(static photo => photo.Created, StringComparer.Ordinal),
            DateDescendingLabel => photos.OrderByDescending(static photo => photo.Created, StringComparer.Ordinal),
            _ => photos.OrderBy(static photo => photo.Title, StringComparer.OrdinalIgnoreCase),
        };
    }

    private void RefreshVisiblePhotos()
    {
        VisiblePhotos.Clear();

        foreach (PhotoItemViewModel photo in _sortedPhotos.Skip(_currentPageIndex * EffectivePageSize).Take(EffectivePageSize))
        {
            VisiblePhotos.Add(photo);
        }

        PageNumberText = CurrentPageNumber.ToString(CultureInfo.InvariantCulture);
    }

    private void MoveToFirstPage() => MoveToPage(0);

    private void MoveToPreviousPage() => MoveToPage(_currentPageIndex - 1);

    private void MoveToNextPage() => MoveToPage(_currentPageIndex + 1);

    private void MoveToLastPage() => MoveToPage(TotalPages - 1);

    private void MoveToPage(int pageIndex)
    {
        if (!HasPhotos)
        {
            PageNumberText = "1";
            return;
        }

        _currentPageIndex = Math.Clamp(pageIndex, 0, TotalPages - 1);
        RefreshVisiblePhotos();
        UpdateNavigationState();
    }

    private void MoveToPreviousSelectedPhoto() => MoveToRelativeSelectedPhoto(-1);

    private void MoveToNextSelectedPhoto() => MoveToRelativeSelectedPhoto(1);

    private void MoveToRelativeSelectedPhoto(int offset)
    {
        int selectedIndex = GetSelectedPhotoIndex();
        if (selectedIndex < 0)
        {
            return;
        }

        int targetIndex = Math.Clamp(selectedIndex + offset, 0, _sortedPhotos.Count - 1);
        if (targetIndex == selectedIndex)
        {
            return;
        }

        SelectedPhoto = _sortedPhotos[targetIndex];
        MoveToPage(GetPageIndexForIndex(targetIndex));
    }

    private int GetSelectedPhotoIndex()
        => SelectedPhoto is null ? -1 : _sortedPhotos.IndexOf(SelectedPhoto);

    private int GetPageIndexForIndex(int photoIndex)
        => photoIndex < 0 ? 0 : photoIndex / EffectivePageSize;

    private int EffectivePageSize => Math.Max(1, PageSize);

    private void UpdateNavigationState()
    {
        OnPropertyChanged(nameof(CurrentPageNumber));
        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(TotalPagesText));
        OnPropertyChanged(nameof(CanMoveToFirstPage));
        OnPropertyChanged(nameof(CanMoveToPreviousPage));
        OnPropertyChanged(nameof(CanMoveToNextPage));
        OnPropertyChanged(nameof(CanMoveToLastPage));
        OnPropertyChanged(nameof(CanMoveToPreviousSelectedPhoto));
        OnPropertyChanged(nameof(CanMoveToNextSelectedPhoto));
        OnPropertyChanged(nameof(HasPhotos));
        OnPropertyChanged(nameof(IsEmptyStateVisible));
        FirstPageCommand.NotifyCanExecuteChanged();
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
        LastPageCommand.NotifyCanExecuteChanged();
        PreviousSelectedPhotoCommand.NotifyCanExecuteChanged();
        NextSelectedPhotoCommand.NotifyCanExecuteChanged();
        ExportSelectedCommand.NotifyCanExecuteChanged();
        ExportSelectedVideoCommand.NotifyCanExecuteChanged();
        ComposeRgbCommand.NotifyCanExecuteChanged();
        ComposeAverageCommand.NotifyCanExecuteChanged();
        ComposeRgbAverageCommand.NotifyCanExecuteChanged();
        ToggleSelectAllCommand.NotifyCanExecuteChanged();
    }

    private void ToggleSelectAll()
    {
        if (!HasPhotos)
        {
            return;
        }

        SetSelectionForAllPhotos(!HasSelectedPhotos);
    }

    private void SetSelectionForAllPhotos(bool isSelected)
    {
        _isUpdatingSelectionInBulk = true;

        try
        {
            foreach (PhotoItemViewModel photo in Photos)
            {
                photo.IsSelected = isSelected;
            }
        }
        finally
        {
            _isUpdatingSelectionInBulk = false;
        }

        _selectionAnchorPhoto = Photos.LastOrDefault(static photo => photo.IsSelected);
        UpdateSelectionState();
    }

    private void ApplySelectionRange(PhotoItemViewModel startPhoto, PhotoItemViewModel endPhoto, bool isSelected)
    {
        List<PhotoItemViewModel> orderedPhotos = _sortedPhotos.Count > 0 ? _sortedPhotos : Photos.ToList();
        int startIndex = orderedPhotos.IndexOf(startPhoto);
        int endIndex = orderedPhotos.IndexOf(endPhoto);
        if (startIndex < 0 || endIndex < 0)
        {
            return;
        }

        int lowerBound = Math.Min(startIndex, endIndex);
        int upperBound = Math.Max(startIndex, endIndex);

        _isUpdatingSelectionInBulk = true;

        try
        {
            for (int index = lowerBound; index <= upperBound; index++)
            {
                orderedPhotos[index].IsSelected = isSelected;
            }
        }
        finally
        {
            _isUpdatingSelectionInBulk = false;
        }
    }

    private void UpdateSelectionState()
    {
        OnPropertyChanged(nameof(HasSelectedPhotos));
        OnPropertyChanged(nameof(ToggleSelectAllButtonText));
        ExportSelectedCommand.NotifyCanExecuteChanged();
        ExportSelectedVideoCommand.NotifyCanExecuteChanged();
        ComposeRgbCommand.NotifyCanExecuteChanged();
        ComposeAverageCommand.NotifyCanExecuteChanged();
        ComposeRgbAverageCommand.NotifyCanExecuteChanged();
        ToggleSelectAllCommand.NotifyCanExecuteChanged();
    }

    private static string FormatUserVisibleException(Exception error)
        => $"{error.GetType().Name}: {error.Message}";
}
