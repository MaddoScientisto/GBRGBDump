using GBTools.ImageSharp.GameBoyCamera.Avalonia.Models;
using GBTools.ImageSharp.GameBoyCamera.Avalonia.ViewModels;
using GBTools.ImageSharp.GameBoyCamera.Model;

namespace GBTools.ImageSharp.GameBoyCamera.Avalonia.Services;

public interface IDialogService
{
    Task<string?> OpenSupportedImageAsync();

    Task<ExportRequest?> SelectExportRequestAsync();

    Task<RgbCompositionRequest?> SelectRgbCompositionRequestAsync(IReadOnlyList<GbcPhoto> sourcePhotos);

    Task<DirectAverageCompositionRequest?> SelectAverageCompositionRequestAsync(IReadOnlyList<GbcPhoto> sourcePhotos);

    Task<SmartAverageCompositionRequest?> SelectRgbAverageCompositionRequestAsync(IReadOnlyList<GbcPhoto> sourcePhotos);

    Task<GbxCartImportRequest?> SelectGbxCartImportRequestAsync(
        IReadOnlyList<GBTools.GBxCart.Serial.GbxCartPortInfo> ports,
        GbxCartImportRequest? initialRequest = null,
        string? errorMessage = null);

    Task<PicoGbPrinterImportRequest?> SelectPicoGbPrinterImportRequestAsync(
        IReadOnlyList<string> ports,
        PicoGbPrinterImportRequest? initialRequest = null,
        string? errorMessage = null);

    Task<PicNRecImportRequest?> SelectPicNRecImportRequestAsync(
        IReadOnlyList<string> ports,
        PicNRecImportRequest? initialRequest = null,
        string? errorMessage = null);

    Task<PicNRecDownloadRequest?> SelectPicNRecDownloadRequestAsync(PicNRecDeviceInfo deviceInfo);

    PicoGbPrinterLogWindowViewModel ShowPicoGbPrinterLogWindow(string title);

    Task<string?> SaveExportFileAsync(ExportFormat format, string suggestedFileNameWithoutExtension);

    Task<VideoExportRequest?> SelectVideoExportRequestAsync(string suggestedFileNameWithoutExtension);

    Task ShowFfmpegOutputAsync(
        string outputPath,
        Func<IProgress<string>, CancellationToken, Task> exportAction);

    Task<string?> PickExportFolderAsync();
}