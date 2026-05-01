using GBTools.ImageSharp.GameBoyCamera.Avalonia.Models;

namespace GBTools.ImageSharp.GameBoyCamera.Avalonia.Services;

public interface IVideoExportService
{
    Task ExportAsync(
        IReadOnlyList<PhotoExportRequest> photos,
        VideoExportRequest request,
        IProgress<string>? consoleOutput = null,
        CancellationToken cancellationToken = default);
}