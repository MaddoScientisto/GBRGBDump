namespace GBTools.ImageSharp.GameBoyCamera.Avalonia.Models;

public sealed record PicNRecPortOption(string? PortName, string DisplayName, string Details);

public sealed record PicNRecImportRequest(string? PortName);

public enum PicNRecTransferTarget
{
    ImportToGallery,
    ExportToFolder,
}

public sealed record PicNRecDeviceInfo(string PortName, int ImageCount, int MaxSupportedImageIndex)
{
    public int LastImageIndex => ImageCount - 1;
}

public sealed record PicNRecDownloadRequest(string PortName, int StartImageNumber, int EndImageNumber, PicNRecTransferTarget Target = PicNRecTransferTarget.ImportToGallery)
{
    public int ImageCount => EndImageNumber - StartImageNumber + 1;
}

public sealed record PicNRecDownloadProgress(
    int CurrentImageNumber,
    int CompletedImageCount,
    int TotalImageCount,
    string Message,
    LoadedPhotoInfo? DownloadedPhoto = null,
    bool ClearsDownloadedPhotos = false);

public sealed record PicNRecDiscoveryProgress(
    string PortName,
    int Attempt,
    string Mode,
    string Message,
    bool? Succeeded = null);