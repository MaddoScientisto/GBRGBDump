using GBTools.ImageSharp.GameBoyCamera.Metadata;

namespace GBTools.ImageSharp.GameBoyCamera.Model;

public sealed record GbcAlbum(
    GameBoyCameraAlbumMetadata? Metadata,
    IReadOnlyList<GbcPhoto> Photos);