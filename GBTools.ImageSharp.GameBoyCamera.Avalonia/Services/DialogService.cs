using Avalonia.Controls;
using Avalonia.Platform.Storage;
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

    public DialogService(MainWindowProvider mainWindowProvider, IBitmapFactory bitmapFactory)
    {
        _mainWindowProvider = mainWindowProvider;
        _bitmapFactory = bitmapFactory;
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

    private Window RequireWindow() => _mainWindowProvider.MainWindow
        ?? throw new InvalidOperationException("The main window is not available yet.");
}