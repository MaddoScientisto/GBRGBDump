using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GBTools.ImageSharp.GameBoyCamera.Avalonia.Models;
using GBTools.ImageSharp.GameBoyCamera.Avalonia.Services;
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
    private readonly ILogger<MainWindowViewModel> _logger;
    private List<PhotoItemViewModel> _sortedPhotos = [];
    private int _currentPageIndex;

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
        ILogger<MainWindowViewModel> logger)
    {
        _albumLoadService = albumLoadService;
        _bitmapFactory = bitmapFactory;
        _dialogService = dialogService;
        _imageExportService = imageExportService;
        _logger = logger;

        Photos = [];
        VisiblePhotos = [];
        LoadImagesCommand = new AsyncRelayCommand(LoadImagesAsync, CanRunCommands);
        ExportSelectedCommand = new AsyncRelayCommand(ExportSelectedAsync, CanExportSelected);
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

    public IAsyncRelayCommand ExportSelectedCommand { get; }

    public IRelayCommand FirstPageCommand { get; }

    public IRelayCommand PreviousPageCommand { get; }

    public IRelayCommand NextPageCommand { get; }

    public IRelayCommand LastPageCommand { get; }

    public IRelayCommand PreviousSelectedPhotoCommand { get; }

    public IRelayCommand NextSelectedPhotoCommand { get; }

    public IReadOnlyList<string> OrderingOptions => SortOptions;

    public bool HasPhotos => Photos.Count > 0;

    public bool IsEmptyStateVisible => !IsLoadingPhotos && !HasPhotos;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasSelection => SelectedPhoto is not null;

    public bool IsSelectionEmpty => SelectedPhoto is null;

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
        ExportSelectedCommand.NotifyCanExecuteChanged();
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
            ErrorMessage = ex.Message;
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

            ClearPhotos();

            foreach (LoadedPhotoInfo photo in loadedAlbum.Photos)
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
            SourceSummary = $"Loaded {Photos.Count} image(s) from {Path.GetFileName(path)} as {loadedAlbum.SourceKind}.";
            OnPropertyChanged(nameof(HasPhotos));
            OnPropertyChanged(nameof(IsEmptyStateVisible));
            ExportSelectedCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex)
        {
            ClearPhotos();
            _logger.LogError(ex, "Failed to load images from {Path}", path);
            ErrorMessage = ex.Message;
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
            ExportSelectedCommand.NotifyCanExecuteChanged();
        }
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
        PageNumberText = "1";
        SelectedPhoto = null;
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
    }
}
