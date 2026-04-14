using GBTools.ImageSharp.GameBoyCamera.Avalonia.Models;

namespace GBTools.ImageSharp.GameBoyCamera.Avalonia.Services;

public interface IDialogService
{
    Task<string?> OpenSupportedImageAsync();

    Task<ExportRequest?> SelectExportRequestAsync();

    Task<string?> SaveExportFileAsync(ExportFormat format, string suggestedFileNameWithoutExtension);

    Task<string?> PickExportFolderAsync();
}