using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GBTools.ImageSharp.GameBoyCamera.Avalonia.Models;
using GBTools.ImageSharp.GameBoyCamera.Avalonia.Services;
using GBTools.ImageSharp.GameBoyCamera.Composition;
using GBTools.ImageSharp.GameBoyCamera.Model;

namespace GBTools.ImageSharp.GameBoyCamera.Avalonia.ViewModels;

public enum CompositionDialogMode
{
    RgbOnly,
    AverageOnly,
    RgbAndAverage,
}

public sealed partial class CompositionDialogViewModel : ViewModelBase, IDisposable
{
    private readonly IReadOnlyList<GbcPhoto> _sourcePhotos;
    private readonly IBitmapFactory _bitmapFactory;
    private readonly RelayCommand _confirmCommand;

    [ObservableProperty]
    private GameBoyCameraCompositionChannelOrder channelOrder = GameBoyCameraCompositionChannelOrder.Sequential;

    [ObservableProperty]
    private GameBoyCameraAverageCompositionMode averageMode = GameBoyCameraAverageCompositionMode.Normal;

    [ObservableProperty]
    private Bitmap? previewBitmap;

    [ObservableProperty]
    private string previewSummary = string.Empty;

    [ObservableProperty]
    private string validationMessage = string.Empty;

    [ObservableProperty]
    private bool canConfirm;

    public CompositionDialogViewModel(CompositionDialogMode mode, IReadOnlyList<GbcPhoto> sourcePhotos, IBitmapFactory bitmapFactory)
    {
        Mode = mode;
        _sourcePhotos = sourcePhotos;
        _bitmapFactory = bitmapFactory;
        _confirmCommand = new RelayCommand(Confirm, () => CanConfirm);
        ConfirmCommand = _confirmCommand;
        CancelCommand = new RelayCommand(Cancel);
        RefreshPreview();
    }

    public CompositionDialogMode Mode { get; }

    public bool IsRgbDialog => Mode != CompositionDialogMode.AverageOnly;

    public bool IsSmartAverageDialog => Mode == CompositionDialogMode.RgbAndAverage;

    public string Title => Mode switch
    {
        CompositionDialogMode.RgbOnly => "RGB Composition",
        CompositionDialogMode.AverageOnly => "Average Composition",
        _ => "RGB + Average Composition",
    };

    public string Description => Mode switch
    {
        CompositionDialogMode.RgbOnly => "Choose the channel order to generate RGB composites from the selected monochrome photos.",
        CompositionDialogMode.AverageOnly => "Preview a direct average over the currently selected photos. This mode is intended as a quick average-only test tool.",
        _ => "Choose the channel order and average mode to generate smart RGB groups and raw-lossless average composites from the selected monochrome photos.",
    };

    public string PreviewTitle => Mode switch
    {
        CompositionDialogMode.RgbOnly => "RGB Preview",
        CompositionDialogMode.AverageOnly => "Average Preview",
        _ => "RGB + Average Preview",
    };

    public bool HasValidationMessage => !string.IsNullOrWhiteSpace(ValidationMessage);

    public IReadOnlyList<GameBoyCameraCompositionChannelOrder> ChannelOrderOptions =>
        Enum.GetValues<GameBoyCameraCompositionChannelOrder>();

    public IReadOnlyList<GameBoyCameraAverageCompositionMode> AverageModeOptions =>
        Enum.GetValues<GameBoyCameraAverageCompositionMode>();

    public IRelayCommand ConfirmCommand { get; }

    public IRelayCommand CancelCommand { get; }

    public event Action<object?>? CloseRequested;

    partial void OnChannelOrderChanged(GameBoyCameraCompositionChannelOrder value) => RefreshPreview();

    partial void OnAverageModeChanged(GameBoyCameraAverageCompositionMode value)
    {
        if (IsSmartAverageDialog)
        {
            RefreshPreview();
        }
    }

    partial void OnPreviewBitmapChanged(Bitmap? oldValue, Bitmap? newValue) => oldValue?.Dispose();

    partial void OnCanConfirmChanged(bool value) => _confirmCommand.NotifyCanExecuteChanged();

    partial void OnValidationMessageChanged(string value) => OnPropertyChanged(nameof(HasValidationMessage));

    public void Dispose()
    {
        PreviewBitmap?.Dispose();
        PreviewBitmap = null;
    }

    private void Confirm()
    {
        if (!CanConfirm)
        {
            return;
        }

        CloseRequested?.Invoke(Mode switch
        {
            CompositionDialogMode.RgbOnly => new RgbCompositionRequest(ChannelOrder),
            CompositionDialogMode.AverageOnly => new DirectAverageCompositionRequest(),
            _ => new SmartAverageCompositionRequest(ChannelOrder, AverageMode),
        });
    }

    private void Cancel() => CloseRequested?.Invoke(null);

    private void RefreshPreview()
    {
        PreviewSummary = string.Empty;
        ValidationMessage = string.Empty;
        CanConfirm = false;
        PreviewBitmap = null;

        if (Mode == CompositionDialogMode.AverageOnly)
        {
            RefreshAverageOnlyPreview();
            return;
        }

        if (_sourcePhotos.Count < 3)
        {
            ValidationMessage = "Select at least 3 monochrome photos to compose.";
            return;
        }

        if (_sourcePhotos.Count % 3 != 0)
        {
            ValidationMessage = $"Selected {_sourcePhotos.Count} monochrome photos. Composition requires a count divisible by 3.";
            return;
        }

        try
        {
            if (IsSmartAverageDialog)
            {
                GameBoyCameraAverageCompositionResult result = GameBoyCameraCompositionService.CreateAveragePhotos(
                    _sourcePhotos,
                    new GameBoyCameraAverageCompositionOptions(ChannelOrder, AverageMode));

                GbcPhoto? previewPhoto = AverageMode == GameBoyCameraAverageCompositionMode.FullBank
                    ? result.AveragePhotos.LastOrDefault()
                    : result.AveragePhotos.FirstOrDefault();

                if (previewPhoto is null)
                {
                    ValidationMessage = "No average preview could be generated from the current selection.";
                    return;
                }

                PreviewBitmap = _bitmapFactory.CreatePreviewBitmap(previewPhoto);
                PreviewSummary = BuildAveragePreviewSummary(result);
            }
            else
            {
                IReadOnlyList<GbcPhoto> result = GameBoyCameraCompositionService.CreateRgbPhotos(
                    _sourcePhotos,
                    new GameBoyCameraRgbCompositionOptions(ChannelOrder));

                PreviewBitmap = _bitmapFactory.CreatePreviewBitmap(result[0]);
                PreviewSummary = $"Creates {result.Count} RGB composite image(s) from {_sourcePhotos.Count} selected monochrome photo(s).";
            }

            CanConfirm = true;
        }
        catch (Exception ex)
        {
            ValidationMessage = ex.Message;
        }
    }

    private void RefreshAverageOnlyPreview()
    {
        if (_sourcePhotos.Count < 3)
        {
            ValidationMessage = "Select at least 3 photos to average.";
            return;
        }

        try
        {
            GbcPhoto averagePhoto = GameBoyCameraCompositionService.CreateDirectAveragePhoto(_sourcePhotos);
            PreviewBitmap = _bitmapFactory.CreatePreviewBitmap(averagePhoto);
            PreviewSummary = $"Creates 1 direct average image from {_sourcePhotos.Count} selected photo(s).";
            CanConfirm = true;
        }
        catch (Exception ex)
        {
            ValidationMessage = ex.Message;
        }
    }

    private string BuildAveragePreviewSummary(GameBoyCameraAverageCompositionResult result)
    {
        string groupSizes = string.Join(" + ", result.SourceGroupSizes.Select(static size => size.ToString()));
        int groupedAverageCount = AverageMode == GameBoyCameraAverageCompositionMode.FullBank
            ? Math.Max(0, result.AveragePhotos.Count - 1)
            : result.AveragePhotos.Count;

        return AverageMode == GameBoyCameraAverageCompositionMode.FullBank
            ? $"Groups {_sourcePhotos.Count} selected raw photo(s) as {groupSizes}, producing {result.RgbPhotos.Count} RGB composite(s), {groupedAverageCount} group average(s), and 1 full-bank average."
            : $"Groups {_sourcePhotos.Count} selected raw photo(s) as {groupSizes}, producing {result.RgbPhotos.Count} RGB composite(s) and {result.AveragePhotos.Count} average image(s).";
    }
}