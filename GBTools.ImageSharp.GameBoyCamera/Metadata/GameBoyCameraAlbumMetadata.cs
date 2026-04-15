namespace GBTools.ImageSharp.GameBoyCamera.Metadata;

public sealed record GameBoyCameraAlbumMetadata(
    GameBoyCameraSourceKind SourceKind,
    string? SourceRomType,
    int FrameCount,
    string? ImportOrdering,
    int? JsonCompatibilityVersion);