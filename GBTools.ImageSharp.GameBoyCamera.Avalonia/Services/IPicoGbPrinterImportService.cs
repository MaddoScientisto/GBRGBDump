using GBTools.ImageSharp.GameBoyCamera.Avalonia.Models;

namespace GBTools.ImageSharp.GameBoyCamera.Avalonia.Services;

public interface IPicoGbPrinterImportService
{
    IReadOnlyList<string> GetAvailablePorts();

    Task<PicoGbPrinterImportResult> ImportAsync(
        PicoGbPrinterImportRequest request,
        IProgress<PicoGbPrinterImportProgress>? progress = null,
        Func<bool>? shouldStop = null,
        CancellationToken cancellationToken = default);
}