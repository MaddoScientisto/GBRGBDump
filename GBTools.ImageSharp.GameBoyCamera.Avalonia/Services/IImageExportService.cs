using GBTools.ImageSharp.GameBoyCamera.Avalonia.Models;

namespace GBTools.ImageSharp.GameBoyCamera.Avalonia.Services;

public interface IImageExportService
{
    Task ExportAsync(IReadOnlyList<PhotoExportRequest> photos, ExportRequest request, string destination, bool destinationIsDirectory, CancellationToken cancellationToken = default);
}