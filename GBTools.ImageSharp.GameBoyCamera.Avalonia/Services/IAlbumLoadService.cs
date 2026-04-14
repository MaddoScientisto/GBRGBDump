using GBTools.ImageSharp.GameBoyCamera.Avalonia.Models;

namespace GBTools.ImageSharp.GameBoyCamera.Avalonia.Services;

public interface IAlbumLoadService
{
    Task<LoadedAlbumResult> LoadAsync(string path, CancellationToken cancellationToken = default);
}