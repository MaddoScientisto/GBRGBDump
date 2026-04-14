using System.Globalization;
using GBTools.ImageSharp.GameBoyCamera.Model;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace GBTools.ImageSharp.GameBoyCamera.Codec;

public static class GameBoyCameraImageCodec
{
    public static GbcTileGrid EncodeToTileGrid(Image<Rgba32> image, GameBoyCameraPalette? palette = null)
    {
        ArgumentNullException.ThrowIfNull(image);

        palette ??= GameBoyCameraPalette.Default;

        int widthInTiles = image.Width / GameBoyCameraConstants.TilePixelWidth;
        int heightInTiles = image.Height / GameBoyCameraConstants.TilePixelHeight;

        if (image.Width % GameBoyCameraConstants.TilePixelWidth != 0 || image.Height % GameBoyCameraConstants.TilePixelHeight != 0)
        {
            throw new ArgumentException("Image dimensions must be multiples of 8 pixels.", nameof(image));
        }

        bool validRaw = widthInTiles == GameBoyCameraConstants.RawPhotoTileWidth && heightInTiles == GameBoyCameraConstants.RawPhotoTileHeight;
        bool validFramed = widthInTiles == GameBoyCameraConstants.FramedPhotoTileWidth && heightInTiles == GameBoyCameraConstants.FramedPhotoTileHeight;
        if (!validRaw && !validFramed)
        {
            throw new ArgumentException("Game Boy Camera images must be either 128x112 or 160x144 pixels.", nameof(image));
        }

        List<GbcTile2Bpp> tiles = new(widthInTiles * heightInTiles);
        byte[] tilePixels = new byte[GameBoyCameraConstants.TilePixelWidth * GameBoyCameraConstants.TilePixelHeight];

        for (int tileY = 0; tileY < heightInTiles; tileY++)
        {
            for (int tileX = 0; tileX < widthInTiles; tileX++)
            {
                int pixelIndex = 0;
                for (int y = 0; y < GameBoyCameraConstants.TilePixelHeight; y++)
                {
                    for (int x = 0; x < GameBoyCameraConstants.TilePixelWidth; x++)
                    {
                        tilePixels[pixelIndex++] = (byte)GetClosestPaletteIndex(image[(tileX * GameBoyCameraConstants.TilePixelWidth) + x, (tileY * GameBoyCameraConstants.TilePixelHeight) + y], palette);
                    }
                }

                tiles.Add(GameBoyCameraTileCodec.EncodeFromColorIndexes(tilePixels));
            }
        }

        return new GbcTileGrid(widthInTiles, heightInTiles, tiles);
    }

    public static byte[] CreateThumbnailBytes(Image<Rgba32> image, GameBoyCameraPalette? palette = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        palette ??= GameBoyCameraPalette.Default;

        const int thumbnailWidth = 32;
        const int thumbnailHeight = 32;
        Image<Rgba32> resized = new(thumbnailWidth, thumbnailHeight);
        try
        {
            for (int y = 0; y < thumbnailHeight; y++)
            {
                int sourceY = (int)Math.Round(y * (image.Height - 1d) / Math.Max(1, thumbnailHeight - 1));
                for (int x = 0; x < thumbnailWidth; x++)
                {
                    int sourceX = (int)Math.Round(x * (image.Width - 1d) / Math.Max(1, thumbnailWidth - 1));
                    resized[x, y] = image[sourceX, sourceY];
                }
            }

            GbcTileGrid grid = EncodeToArbitraryTileGrid(resized, palette);
            byte[] result = new byte[GbcThumbnail.ByteLength];
            for (int index = 0; index < grid.Tiles.Count; index++)
            {
                grid.Tiles[index].Bytes.Span.CopyTo(result.AsSpan(index * GameBoyCameraConstants.TileByteCount, GameBoyCameraConstants.TileByteCount));
            }

            return result;
        }
        finally
        {
            resized.Dispose();
        }
    }

    public static Image<Rgba32> RenderFrames(IReadOnlyList<GbcTileGrid> grids, GameBoyCameraPalette? palette = null)
    {
        ArgumentNullException.ThrowIfNull(grids);

        if (grids.Count == 0)
        {
            throw new ArgumentException("At least one frame is required.", nameof(grids));
        }

        Image<Rgba32>? result = null;
        foreach (GbcTileGrid grid in grids)
        {
            using Image<Rgba32> rendered = GameBoyCameraTileGridRenderer.Render(grid, palette);
            if (result is null)
            {
                result = rendered.Clone();
                continue;
            }

            result.Frames.AddFrame(rendered.Frames.RootFrame);
        }

        return result!;
    }

    public static Image<Rgba32> RenderAlbum(GbcAlbum album, GameBoyCameraPalette? palette = null)
    {
        ArgumentNullException.ThrowIfNull(album);

        if (album.Photos.Count == 0)
        {
            throw new ArgumentException("Album must contain at least one photo.", nameof(album));
        }

        return RenderFrames(album.Photos.Select(static photo => photo.TileGrid).ToArray(), palette);
    }

    public static IReadOnlyList<string> FormatTiles(GbcTileGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        return grid.Tiles.Select(GameBoyCameraTileTextCodec.FormatTile).ToArray();
    }

    public static GbcTileGrid ParseBinaryTilePayload(ReadOnlySpan<byte> data, int? preferredWidthInTiles = null)
    {
        if (data.Length % GameBoyCameraConstants.TileByteCount != 0)
        {
            throw new ArgumentException("Binary tile payload length must be divisible by 16.", nameof(data));
        }

        int tileCount = data.Length / GameBoyCameraConstants.TileByteCount;
        int widthInTiles = preferredWidthInTiles ?? GuessWidthInTiles(tileCount);
        return GameBoyCameraTileGridFactory.CreateFromBinary(data, widthInTiles);
    }

    private static GbcTileGrid EncodeToArbitraryTileGrid(Image<Rgba32> image, GameBoyCameraPalette palette)
    {
        int widthInTiles = image.Width / GameBoyCameraConstants.TilePixelWidth;
        int heightInTiles = image.Height / GameBoyCameraConstants.TilePixelHeight;
        List<GbcTile2Bpp> tiles = new(widthInTiles * heightInTiles);
        byte[] tilePixels = new byte[GameBoyCameraConstants.TilePixelWidth * GameBoyCameraConstants.TilePixelHeight];

        for (int tileY = 0; tileY < heightInTiles; tileY++)
        {
            for (int tileX = 0; tileX < widthInTiles; tileX++)
            {
                int pixelIndex = 0;
                for (int y = 0; y < GameBoyCameraConstants.TilePixelHeight; y++)
                {
                    for (int x = 0; x < GameBoyCameraConstants.TilePixelWidth; x++)
                    {
                        tilePixels[pixelIndex++] = (byte)GetClosestPaletteIndex(image[(tileX * GameBoyCameraConstants.TilePixelWidth) + x, (tileY * GameBoyCameraConstants.TilePixelHeight) + y], palette);
                    }
                }

                tiles.Add(GameBoyCameraTileCodec.EncodeFromColorIndexes(tilePixels));
            }
        }

        return new GbcTileGrid(widthInTiles, heightInTiles, tiles);
    }

    private static int GetClosestPaletteIndex(Rgba32 color, GameBoyCameraPalette palette)
    {
        int bestIndex = 0;
        int bestDistance = int.MaxValue;
        for (int index = 0; index < palette.Colors.Count; index++)
        {
            Rgba32 paletteColor = palette[index];
            int red = color.R - paletteColor.R;
            int green = color.G - paletteColor.G;
            int blue = color.B - paletteColor.B;
            int alpha = color.A - paletteColor.A;
            int distance = (red * red) + (green * green) + (blue * blue) + (alpha * alpha);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = index;
            }
        }

        return bestIndex;
    }

    private static int GuessWidthInTiles(int tileCount)
    {
        if (tileCount == GameBoyCameraConstants.RawPhotoTileWidth * GameBoyCameraConstants.RawPhotoTileHeight)
        {
            return GameBoyCameraConstants.RawPhotoTileWidth;
        }

        if (tileCount == GameBoyCameraConstants.FramedPhotoTileWidth * GameBoyCameraConstants.FramedPhotoTileHeight)
        {
            return GameBoyCameraConstants.FramedPhotoTileWidth;
        }

        if (tileCount % GameBoyCameraConstants.RawPhotoTileWidth == 0)
        {
            return GameBoyCameraConstants.RawPhotoTileWidth;
        }

        if (tileCount % GameBoyCameraConstants.FramedPhotoTileWidth == 0)
        {
            return GameBoyCameraConstants.FramedPhotoTileWidth;
        }

        throw new InvalidDataException(string.Create(CultureInfo.InvariantCulture, $"Unable to infer a tile grid width for {tileCount} tiles."));
    }
}