using GBTools.ImageSharp.GameBoyCamera.Avalonia.Models;
using GBTools.ImageSharp.GameBoyCamera.Model;

namespace GBTools.ImageSharp.GameBoyCamera.Avalonia.Services;

public interface IDialogService
{
    Task<string?> OpenSupportedImageAsync();

    Task<ExportRequest?> SelectExportRequestAsync();

    Task<RgbCompositionRequest?> SelectRgbCompositionRequestAsync(IReadOnlyList<GbcPhoto> sourcePhotos);

    Task<DirectAverageCompositionRequest?> SelectAverageCompositionRequestAsync(IReadOnlyList<GbcPhoto> sourcePhotos);

    Task<SmartAverageCompositionRequest?> SelectRgbAverageCompositionRequestAsync(IReadOnlyList<GbcPhoto> sourcePhotos);

    Task<string?> SaveExportFileAsync(ExportFormat format, string suggestedFileNameWithoutExtension);

    Task<string?> PickExportFolderAsync();
}