using GBTools.GBxCart.Serial;

namespace GBTools.ImageSharp.GameBoyCamera.Avalonia.Models;

public sealed record GbxCartPortOption(string? PortName, string DisplayName, string Details);

public sealed record GbxCartImportRequest(
    string? PortName,
    GbxCartDumpMode Mode,
    bool IgnoreDeletedPhotos,
    bool IgnoreLastSeenPhoto,
    bool AcceptBadDumps);

public sealed record GbxCartImportProgress(string Message, double CompletedSteps, double TotalSteps);

public sealed record GbxCartImportResult(string PortName, LoadedAlbumResult Album);