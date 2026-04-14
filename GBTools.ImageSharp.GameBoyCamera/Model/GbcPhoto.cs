using GBTools.ImageSharp.GameBoyCamera.Metadata;

namespace GBTools.ImageSharp.GameBoyCamera.Model;

public sealed record GbcPhoto(
    GbcTileGrid TileGrid,
    GbcFrameOverlay? FrameOverlay,
    GameBoyCameraFrameMetadata? Metadata,
    GbcThumbnail? Thumbnail);