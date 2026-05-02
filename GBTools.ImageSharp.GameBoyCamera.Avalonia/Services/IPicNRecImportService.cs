using GBTools.ImageSharp.GameBoyCamera.Avalonia.Models;

namespace GBTools.ImageSharp.GameBoyCamera.Avalonia.Services;

public interface IPicNRecImportService
{
    IReadOnlyList<string> GetAvailablePorts();

    Task<PicNRecDeviceInfo> DetectAsync(
        string? portName = null,
        IProgress<PicNRecDiscoveryProgress>? progress = null,
        IProgress<string>? serialLog = null,
        CancellationToken cancellationToken = default);

    Task<LoadedAlbumResult> DownloadImagesAsync(
        PicNRecDownloadRequest request,
        IProgress<PicNRecDownloadProgress>? progress = null,
        IProgress<string>? serialLog = null,
        Func<LoadedPhotoInfo, CancellationToken, Task>? onPhotoLoaded = null,
        CancellationToken cancellationToken = default);

    Task<LoadedPhotoInfo> PreviewImageAsync(
        string portName,
        int imageNumber,
        IProgress<string>? serialLog = null,
        CancellationToken cancellationToken = default);

    Task ClearLastImageMarkerAsync(
        string portName,
        IProgress<string>? serialLog = null,
        CancellationToken cancellationToken = default);
}