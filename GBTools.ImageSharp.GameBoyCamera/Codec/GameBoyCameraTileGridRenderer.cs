using GBTools.ImageSharp.GameBoyCamera.Model;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace GBTools.ImageSharp.GameBoyCamera.Codec;

public static class GameBoyCameraTileGridRenderer
{
    public static Image<Rgba32> Render(GbcTileGrid grid, GameBoyCameraPalette? palette = null)
    {
        ArgumentNullException.ThrowIfNull(grid);

        palette ??= GameBoyCameraPalette.Default;

        int width = grid.WidthInTiles * GameBoyCameraConstants.TilePixelWidth;
        int height = grid.HeightInTiles * GameBoyCameraConstants.TilePixelHeight;
        Image<Rgba32> image = new(width, height);

        for (int tileIndex = 0; tileIndex < grid.Tiles.Count; tileIndex++)
        {
            int tileX = tileIndex % grid.WidthInTiles;
            int tileY = tileIndex / grid.WidthInTiles;
            byte[] pixels = GameBoyCameraTileCodec.DecodeToColorIndexes(grid.Tiles[tileIndex]);

            for (int y = 0; y < GameBoyCameraConstants.TilePixelHeight; y++)
            {
                for (int x = 0; x < GameBoyCameraConstants.TilePixelWidth; x++)
                {
                    int pixelIndex = (y * GameBoyCameraConstants.TilePixelWidth) + x;
                    image[(tileX * GameBoyCameraConstants.TilePixelWidth) + x, (tileY * GameBoyCameraConstants.TilePixelHeight) + y] = palette[pixels[pixelIndex]];
                }
            }
        }

        return image;
    }
}