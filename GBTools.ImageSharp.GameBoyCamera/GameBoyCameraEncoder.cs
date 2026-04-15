using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using GBTools.ImageSharp.GameBoyCamera.Codec;
using GBTools.ImageSharp.GameBoyCamera.Model;

namespace GBTools.ImageSharp.GameBoyCamera;

public sealed class GameBoyCameraEncoder : ImageEncoder
{
    protected override void Encode<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(stream);

        using Image<Rgba32> rgba = image.CloneAs<Rgba32>();
        GbcTileGrid grid = GameBoyCameraImageCodec.EncodeToTileGrid(rgba, GameBoyCameraPalette.Default);
        byte[] thumbnail = GameBoyCameraImageCodec.CreateThumbnailBytes(rgba, GameBoyCameraPalette.Default);

        using BinaryWriter writer = new(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write(GameBoyCameraConstants.CanonicalMagicHeader);
        writer.Write((byte)1);
        writer.Write((byte)1);
        writer.Write((byte)grid.WidthInTiles);
        writer.Write((byte)grid.HeightInTiles);
        writer.Write(grid.Tiles.Count * GameBoyCameraConstants.TileByteCount);
        writer.Write(thumbnail.Length);
        writer.Write((short)0);

        foreach (GbcTile2Bpp tile in grid.Tiles)
        {
            writer.Write(tile.Bytes.Span);
        }

        writer.Write(thumbnail);
    }
}