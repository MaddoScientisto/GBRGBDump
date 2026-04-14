using GBTools.ImageSharp.GameBoyCamera.Metadata;

namespace GBTools.ImageSharp.GameBoyCamera.Model;

public sealed record GbcPhoto(
    GbcTileGrid TileGrid,
    GbcFrameOverlay? FrameOverlay,
    GameBoyCameraFrameMetadata? Metadata,
    GbcThumbnail? Thumbnail,
    ReadOnlyMemory<byte>? RenderedRgbaPixels = null,
    int RenderedWidth = 0,
    int RenderedHeight = 0,
    GbcRgbnData? RgbnData = null)
{
    public bool HasRenderedImage => RenderedRgbaPixels is { } pixels
        && !pixels.IsEmpty
        && RenderedWidth > 0
        && RenderedHeight > 0;
}

public sealed record GbcRgbnData(
    string CompositeHash,
    GbcRgbnChannelData? Red,
    GbcRgbnChannelData? Green,
    GbcRgbnChannelData? Blue,
    GbcRgbnChannelData? Neutral,
    IReadOnlyList<byte> RedPalette,
    IReadOnlyList<byte> GreenPalette,
    IReadOnlyList<byte> BluePalette,
    IReadOnlyList<byte> NeutralPalette,
    string BlendMode);

public sealed record GbcRgbnChannelData(string Hash, string CompressedPayload);