using GBTools.ImageSharp.GameBoyCamera.Avalonia.Models;

namespace GBTools.ImageSharp.GameBoyCamera.Avalonia.Services;

public interface IAlbumLoadService
{
    Task<LoadedAlbumResult> LoadAsync(string path, CancellationToken cancellationToken = default);

    Task<LoadedAlbumResult> LoadPicoGbPrinterCaptureAsync(
        byte[] captureData,
        string captureName,
        CancellationToken cancellationToken = default);
}