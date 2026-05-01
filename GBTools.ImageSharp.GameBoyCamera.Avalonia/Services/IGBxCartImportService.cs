using GBTools.GBxCart.Serial;
using GBTools.ImageSharp.GameBoyCamera.Avalonia.Models;

namespace GBTools.ImageSharp.GameBoyCamera.Avalonia.Services;

public interface IGBxCartImportService
{
    IReadOnlyList<GbxCartPortInfo> GetAvailablePorts();

    Task<GbxCartImportResult> ImportAsync(
        GbxCartImportRequest request,
        IProgress<GbxCartImportProgress>? progress = null,
        CancellationToken cancellationToken = default);
}