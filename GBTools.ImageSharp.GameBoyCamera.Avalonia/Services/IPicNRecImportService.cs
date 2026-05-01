using GBTools.ImageSharp.GameBoyCamera.Avalonia.Models;

namespace GBTools.ImageSharp.GameBoyCamera.Avalonia.Services;

public interface IPicNRecImportService
{
    Task<PicNRecDeviceInfo> DetectAsync(
        IProgress<PicNRecDiscoveryProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<LoadedAlbumResult> DownloadImagesAsync(
        PicNRecDownloadRequest request,
        IProgress<PicNRecDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<LoadedPhotoInfo> PreviewImageAsync(
        string portName,
        int imageNumber,
        CancellationToken cancellationToken = default);
}