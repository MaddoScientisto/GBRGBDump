namespace GBTools.ImageSharp.GameBoyCamera.Avalonia.Services;

public interface IAppSettingsService
{
    string? LastVideoExportPath { get; set; }

    string? LastGbxCartPortName { get; set; }

    string? LastGbxCartMode { get; set; }

    string? LastPicoGbPrinterPortName { get; set; }

    string? LastPicoGbPrinterMode { get; set; }

    bool IgnoreDeletedPhotosForGbxCart { get; set; }

    bool IgnoreLastSeenPhotoForGbxCart { get; set; }

    bool AcceptBadDumpsForGbxCart { get; set; }

    void Save();
}