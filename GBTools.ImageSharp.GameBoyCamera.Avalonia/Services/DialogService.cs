using Avalonia.Controls;
using Avalonia.Platform.Storage;
using GBTools.GBxCart.Serial;
using GBTools.ImageSharp.GameBoyCamera.Avalonia.Infrastructure;
using GBTools.ImageSharp.GameBoyCamera.Avalonia.Models;
using GBTools.ImageSharp.GameBoyCamera.Avalonia.ViewModels;
using GBTools.ImageSharp.GameBoyCamera.Model;
using GBTools.ImageSharp.GameBoyCamera.Avalonia.Views;

namespace GBTools.ImageSharp.GameBoyCamera.Avalonia.Services;

public sealed class DialogService : IDialogService
{
    private static readonly FilePickerFileType SupportedImports = new(
        "Supported Game Boy Camera Files")
    {
        Patterns = ["*.gbci", "*.sav", "*.gb", "*.gbc", "*.bin", "*.json"],
    };

    private readonly MainWindowProvider _mainWindowProvider;
    private readonly IBitmapFactory _bitmapFactory;
    private readonly IPicNRecImportService _picNRecImportService;
    private readonly IAppSettingsService _settings;
    private PicoGbPrinterLogWindow? _picoGbPrinterLogWindow;

    public DialogService(
        MainWindowProvider mainWindowProvider,
        IBitmapFactory bitmapFactory,
        IPicNRecImportService picNRecImportService,
        IAppSettingsService settings)
    {
        _mainWindowProvider = mainWindowProvider;
        _bitmapFactory = bitmapFactory;
        _picNRecImportService = picNRecImportService;
        _settings = settings;
    }

    public async Task<string?> OpenSupportedImageAsync()
    {
        IReadOnlyList<IStorageFile> files = await RequireWindow().StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            Title = "Open Game Boy Camera image source",
            FileTypeFilter = [SupportedImports],
        }).ConfigureAwait(true);

        return files.FirstOrDefault()?.TryGetLocalPath();
    }

    public async Task<ExportRequest?> SelectExportRequestAsync()
    {
        ExportFormatDialog dialog = new()
        {
            DataContext = new ExportFormatDialogViewModel(),
        };

        ExportRequest? request = await dialog.ShowDialog<ExportRequest?>(RequireWindow()).ConfigureAwait(true);
        if (request is null || request.Format != ExportFormat.Png)
        {
            return request;
        }

        PngMagnificationDialog magnificationDialog = new()
        {
            DataContext = new PngMagnificationDialogViewModel(),
        };

        int? magnification = await magnificationDialog.ShowDialog<int?>(RequireWindow()).ConfigureAwait(true);
        return magnification is null
            ? null
            : request with { PngMagnification = magnification.Value };
    }

    public Task<RgbCompositionRequest?> SelectRgbCompositionRequestAsync(IReadOnlyList<GbcPhoto> sourcePhotos)
    {
        CompositionDialog dialog = new()
        {
            DataContext = new CompositionDialogViewModel(CompositionDialogMode.RgbOnly, sourcePhotos, _bitmapFactory),
        };

        return dialog.ShowDialog<RgbCompositionRequest?>(RequireWindow());
    }

    public Task<DirectAverageCompositionRequest?> SelectAverageCompositionRequestAsync(IReadOnlyList<GbcPhoto> sourcePhotos)
    {
        CompositionDialog dialog = new()
        {
            DataContext = new CompositionDialogViewModel(CompositionDialogMode.AverageOnly, sourcePhotos, _bitmapFactory),
        };

        return dialog.ShowDialog<DirectAverageCompositionRequest?>(RequireWindow());
    }

    public Task<SmartAverageCompositionRequest?> SelectRgbAverageCompositionRequestAsync(IReadOnlyList<GbcPhoto> sourcePhotos)
    {
        CompositionDialog dialog = new()
        {
            DataContext = new CompositionDialogViewModel(CompositionDialogMode.RgbAndAverage, sourcePhotos, _bitmapFactory),
        };

        return dialog.ShowDialog<SmartAverageCompositionRequest?>(RequireWindow());
    }

    public Task<GbxCartImportRequest?> SelectGbxCartImportRequestAsync(
        IReadOnlyList<GbxCartPortInfo> ports,
        GbxCartImportRequest? initialRequest = null,
        string? errorMessage = null)
    {
        GbxCartImportRequest seedRequest = initialRequest ?? CreateStoredGbxCartRequest();
        GbxCartImportDialog dialog = new()
        {
            DataContext = new GbxCartImportDialogViewModel(ports, seedRequest, errorMessage),
        };

        return ShowGbxCartDialogAndPersistAsync(dialog);
    }

    public Task<PicoGbPrinterImportRequest?> SelectPicoGbPrinterImportRequestAsync(
        IReadOnlyList<string> ports,
        PicoGbPrinterImportRequest? initialRequest = null,
        string? errorMessage = null)
    {
        PicoGbPrinterImportRequest seedRequest = initialRequest ?? CreateStoredPicoGbPrinterRequest();
        PicoGbPrinterImportDialog dialog = new()
        {
            DataContext = new PicoGbPrinterImportDialogViewModel(ports, seedRequest, errorMessage),
        };

        return ShowPicoGbPrinterDialogAndPersistAsync(dialog);
    }

    public Task<PicNRecDownloadRequest?> SelectPicNRecDownloadRequestAsync(PicNRecDeviceInfo deviceInfo)
    {
        PicNRecRangeDialog dialog = new()
        {
            DataContext = new PicNRecRangeDialogViewModel(deviceInfo, _picNRecImportService, _bitmapFactory),
        };

        return dialog.ShowDialog<PicNRecDownloadRequest?>(RequireWindow());
    }

    public PicoGbPrinterLogWindowViewModel ShowPicoGbPrinterLogWindow(string title)
    {
        if (_picoGbPrinterLogWindow?.DataContext is PicoGbPrinterLogWindowViewModel existingViewModel)
        {
            existingViewModel.Reset(title);
            _picoGbPrinterLogWindow.Title = title;
            _picoGbPrinterLogWindow.Activate();
            return existingViewModel;
        }

        PicoGbPrinterLogWindowViewModel viewModel = new(title);
        PicoGbPrinterLogWindow window = new()
        {
            DataContext = viewModel,
            Title = title,
        };

        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_picoGbPrinterLogWindow, window))
            {
                _picoGbPrinterLogWindow = null;
            }
        };

        _picoGbPrinterLogWindow = window;
        window.Show(RequireWindow());
        return viewModel;
    }

    public async Task<string?> SaveExportFileAsync(ExportFormat format, string suggestedFileNameWithoutExtension)
    {
        IStorageFile? file = await RequireWindow().StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export decoded image",
            SuggestedFileName = $"{suggestedFileNameWithoutExtension}.{format.GetDefaultExtension()}",
            DefaultExtension = format.GetDefaultExtension(),
            FileTypeChoices = [CreateExportFileType(format)],
        }).ConfigureAwait(true);

        return file?.TryGetLocalPath();
    }

    public async Task<VideoExportRequest?> SelectVideoExportRequestAsync(string suggestedFileNameWithoutExtension)
    {
        VideoExportDialogViewModel viewModel = new()
        {
            OutputPath = _settings.LastVideoExportPath ?? string.Empty,
        };
        VideoExportDialog dialog = new()
        {
            DataContext = viewModel,
        };

        viewModel.BrowseRequested += async () =>
        {
            IStorageFile? file = await RequireWindow().StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export selected images as video",
                SuggestedFileName = CreateSuggestedVideoFileName(suggestedFileNameWithoutExtension),
                DefaultExtension = "mp4",
                FileTypeChoices = [new FilePickerFileType("MP4 video") { Patterns = ["*.mp4"] }],
            }).ConfigureAwait(true);

            string? outputPath = file?.TryGetLocalPath();
            if (!string.IsNullOrWhiteSpace(outputPath))
            {
                viewModel.OutputPath = outputPath;
            }
        };

        VideoExportRequest? request = await dialog.ShowDialog<VideoExportRequest?>(RequireWindow()).ConfigureAwait(true);
        if (request is null)
        {
            return null;
        }

        if (File.Exists(request.OutputPath))
        {
            bool overwrite = await ConfirmAsync(
                "Replace Video File",
                $"A file already exists at:{Environment.NewLine}{request.OutputPath}{Environment.NewLine}{Environment.NewLine}Replace it?",
                "Replace",
                "Cancel").ConfigureAwait(true);
            if (!overwrite)
            {
                return null;
            }
        }

        _settings.LastVideoExportPath = request.OutputPath;
        _settings.Save();
        return request;
    }

    public async Task ShowFfmpegOutputAsync(
        string outputPath,
        Func<IProgress<string>, CancellationToken, Task> exportAction)
    {
        ArgumentNullException.ThrowIfNull(exportAction);

        FfmpegOutputDialogViewModel viewModel = new(outputPath);
        FfmpegOutputDialog dialog = new()
        {
            DataContext = viewModel,
        };

        Exception? exportError = null;
        dialog.Opened += async (_, _) =>
        {
            Progress<string> progress = new(viewModel.AppendLine);
            try
            {
                await exportAction(progress, CancellationToken.None).ConfigureAwait(true);
            }
            catch (Exception error)
            {
                exportError = error;
                viewModel.AppendLine($"ERROR: {error}");
            }
            finally
            {
                viewModel.MarkFinished(exportError);
            }
        };

        await dialog.ShowDialog(RequireWindow()).ConfigureAwait(true);
        if (exportError is not null)
        {
            throw new InvalidOperationException("ffmpeg video export failed. See the ffmpeg output window for details.", exportError);
        }
    }

    public async Task<string?> PickExportFolderAsync()
    {
        IReadOnlyList<IStorageFolder> folders = await RequireWindow().StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            Title = "Choose export folder",
        }).ConfigureAwait(true);

        return folders.FirstOrDefault()?.TryGetLocalPath();
    }

    private static FilePickerFileType CreateExportFileType(ExportFormat format) => new(format.GetDisplayName())
    {
        Patterns = [$"*.{format.GetDefaultExtension()}"]
    };

    private async Task<bool> ConfirmAsync(string title, string message, string confirmText, string cancelText)
    {
        ConfirmationDialog dialog = new()
        {
            DataContext = new ConfirmationDialogViewModel(title, message, confirmText, cancelText),
        };

        return await dialog.ShowDialog<bool>(RequireWindow()).ConfigureAwait(true);
    }

    private GbxCartImportRequest CreateStoredGbxCartRequest()
    {
        GbxCartDumpMode mode = Enum.TryParse(_settings.LastGbxCartMode, ignoreCase: true, out GbxCartDumpMode parsedMode)
            ? parsedMode
            : GbxCartDumpMode.Save;

        return new GbxCartImportRequest(
            _settings.LastGbxCartPortName,
            mode,
            _settings.IgnoreDeletedPhotosForGbxCart,
            _settings.IgnoreLastSeenPhotoForGbxCart,
            _settings.AcceptBadDumpsForGbxCart);
    }

    private PicoGbPrinterImportRequest CreateStoredPicoGbPrinterRequest()
    {
        PicoGbPrinterImportMode mode = Enum.TryParse(_settings.LastPicoGbPrinterMode, ignoreCase: true, out PicoGbPrinterImportMode parsedMode)
            ? parsedMode
            : PicoGbPrinterImportMode.WaitForNextCapture;

        return new PicoGbPrinterImportRequest(_settings.LastPicoGbPrinterPortName, mode);
    }

    private async Task<GbxCartImportRequest?> ShowGbxCartDialogAndPersistAsync(GbxCartImportDialog dialog)
    {
        GbxCartImportRequest? request = await dialog.ShowDialog<GbxCartImportRequest?>(RequireWindow()).ConfigureAwait(true);
        if (request is null)
        {
            return null;
        }

        _settings.LastGbxCartPortName = request.PortName;
        _settings.LastGbxCartMode = request.Mode.ToString();
        _settings.IgnoreDeletedPhotosForGbxCart = request.IgnoreDeletedPhotos;
        _settings.IgnoreLastSeenPhotoForGbxCart = request.IgnoreLastSeenPhoto;
        _settings.AcceptBadDumpsForGbxCart = request.AcceptBadDumps;
        _settings.Save();
        return request;
    }

    private async Task<PicoGbPrinterImportRequest?> ShowPicoGbPrinterDialogAndPersistAsync(PicoGbPrinterImportDialog dialog)
    {
        PicoGbPrinterImportRequest? request = await dialog.ShowDialog<PicoGbPrinterImportRequest?>(RequireWindow()).ConfigureAwait(true);
        if (request is null)
        {
            return null;
        }

        _settings.LastPicoGbPrinterPortName = request.PortName;
        _settings.LastPicoGbPrinterMode = request.Mode.ToString();
        _settings.Save();
        return request;
    }

    private string CreateSuggestedVideoFileName(string suggestedFileNameWithoutExtension)
    {
        string? rememberedPath = _settings.LastVideoExportPath;
        if (!string.IsNullOrWhiteSpace(rememberedPath))
        {
            string rememberedName = Path.GetFileName(rememberedPath);
            if (!string.IsNullOrWhiteSpace(rememberedName))
            {
                return rememberedName;
            }
        }

        return $"{suggestedFileNameWithoutExtension}.mp4";
    }

    private Window RequireWindow() => _mainWindowProvider.MainWindow
        ?? throw new InvalidOperationException("The main window is not available yet.");
}