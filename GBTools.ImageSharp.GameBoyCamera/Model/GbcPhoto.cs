using GBTools.ImageSharp.GameBoyCamera.Composition;
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
    GbcRgbnData? RgbnData = null,
    GbcAverageData? AverageData = null)
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

public sealed record GbcAverageData(
    string CompositeHash,
    IReadOnlyList<GbcAverageSourceGroup> SourceGroups,
    GameBoyCameraCompositionChannelOrder ChannelOrder,
    string Algorithm,
    GameBoyCameraAverageCompositionPipeline Pipeline)
{
    public int SourcePhotoCount => SourceGroups.Sum(static group => group.SourcePhotos.Count);
}

public enum GameBoyCameraAverageCompositionPipeline
{
    Direct,
    Rgb,
}

public sealed record GbcAverageSourceGroup(IReadOnlyList<GbcAverageSourcePhoto> SourcePhotos);

public sealed record GbcAverageSourcePhoto(string Hash, int TileCount, string CompressedPayload);