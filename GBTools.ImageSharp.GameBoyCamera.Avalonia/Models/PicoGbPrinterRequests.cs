namespace GBTools.ImageSharp.GameBoyCamera.Avalonia.Models;

public enum PicoGbPrinterImportMode
{
    WaitForNextCapture,
    LoadLastCapture,
}

public sealed record PicoGbPrinterPortOption(string? PortName, string DisplayName, string Details);

public sealed record PicoGbPrinterImportRequest(string? PortName, PicoGbPrinterImportMode Mode);

public sealed record PicoGbPrinterImportProgress(
    string Message,
    double CompletedSteps,
    double TotalSteps,
    bool AppendToLog = true,
    IReadOnlyList<LoadedPhotoInfo>? ReceivedPhotos = null);

public sealed record PicoGbPrinterImportResult(
    string PortName,
    LoadedAlbumResult Album,
    int CaptureCount,
    int CaptureByteCount,
    bool IsReplay,
    bool IsTransfer);