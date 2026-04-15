using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace GBTools.ImageSharp.GameBoyCamera.Lite;

public static class GameBoyCameraBinDecoder
{
    private static ReadOnlySpan<byte> GbBinHeader => "GB-BIN01"u8;
    private const int TileByteCount = 16;
    private const int TilePixelWidth = 8;
    private const int TilePixelHeight = 8;
    private const int RawPhotoTileWidth = 16;
    private const int RawPhotoTileHeight = 14;
    private const int FramedPhotoTileWidth = 20;
    private const int FramedPhotoTileHeight = 18;

    private static readonly Rgba32[] DefaultPalette =
    [
        new(255, 255, 255, 255),
        new(170, 170, 170, 255),
        new(85, 85, 85, 255),
        new(0, 0, 0, 255),
    ];

    public static Image<Rgba32> Decode(byte[] data, int? widthInTiles = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        return Decode(data.AsSpan(), widthInTiles);
    }

    public static Image<Rgba32> Decode(ReadOnlySpan<byte> data, int? widthInTiles = null)
    {
        if (data.StartsWith(GbBinHeader))
        {
            data = data[GbBinHeader.Length..];
        }

        if (data.Length % TileByteCount != 0)
        {
            throw new ArgumentException("Binary tile payload length must be divisible by 16.", nameof(data));
        }

        int tileCount = data.Length / TileByteCount;
        int resolvedWidthInTiles = widthInTiles ?? GuessWidthInTiles(tileCount);
        if (resolvedWidthInTiles <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(widthInTiles), "Grid width must be greater than zero.");
        }

        if (tileCount % resolvedWidthInTiles != 0)
        {
            throw new ArgumentException("Tile count must divide evenly by the grid width.", nameof(widthInTiles));
        }

        int heightInTiles = tileCount / resolvedWidthInTiles;
        Image<Rgba32> image = new(resolvedWidthInTiles * TilePixelWidth, heightInTiles * TilePixelHeight);

        for (int tileIndex = 0; tileIndex < tileCount; tileIndex++)
        {
            int tileX = tileIndex % resolvedWidthInTiles;
            int tileY = tileIndex / resolvedWidthInTiles;
            ReadOnlySpan<byte> tile = data.Slice(tileIndex * TileByteCount, TileByteCount);
            DrawTile(image, tileX, tileY, tile);
        }

        return image;
    }

    private static void DrawTile(Image<Rgba32> image, int tileX, int tileY, ReadOnlySpan<byte> tile)
    {
        for (int y = 0; y < TilePixelHeight; y++)
        {
            byte low = tile[y * 2];
            byte high = tile[(y * 2) + 1];

            for (int x = 0; x < TilePixelWidth; x++)
            {
                int shift = 7 - x;
                int colorIndex = (((high >> shift) & 0x01) << 1) | ((low >> shift) & 0x01);
                image[(tileX * TilePixelWidth) + x, (tileY * TilePixelHeight) + y] = DefaultPalette[colorIndex];
            }
        }
    }

    private static int GuessWidthInTiles(int tileCount)
    {
        if (tileCount == RawPhotoTileWidth * RawPhotoTileHeight)
        {
            return RawPhotoTileWidth;
        }

        if (tileCount == FramedPhotoTileWidth * FramedPhotoTileHeight)
        {
            return FramedPhotoTileWidth;
        }

        if (tileCount % RawPhotoTileWidth == 0 && (tileCount / RawPhotoTileWidth) == RawPhotoTileHeight)
        {
            return RawPhotoTileWidth;
        }

        if (tileCount % FramedPhotoTileWidth == 0 && (tileCount / FramedPhotoTileWidth) == FramedPhotoTileHeight)
        {
            return FramedPhotoTileWidth;
        }

        throw new InvalidDataException($"Unable to infer a tile grid width for {tileCount} tiles. Pass widthInTiles explicitly.");
    }
}