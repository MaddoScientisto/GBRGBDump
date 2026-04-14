using GBTools.ImageSharp.GameBoyCamera.Model;

namespace GBTools.ImageSharp.GameBoyCamera.Codec;

public static class GameBoyCameraFrameCodec
{
    public static GbcFrameOverlay ExtractFrameOverlay(GbcTileGrid grid, int imageStartLine)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentOutOfRangeException.ThrowIfNegative(imageStartLine);

        if (grid.WidthInTiles != GameBoyCameraConstants.FramedPhotoTileWidth)
        {
            throw new ArgumentException("Frame extraction expects a 20-tile-wide grid.", nameof(grid));
        }

        if (grid.HeightInTiles < imageStartLine + GameBoyCameraConstants.RawPhotoTileHeight)
        {
            throw new ArgumentException("Grid height is too small for the requested frame extraction start line.", nameof(grid));
        }

        IReadOnlyList<GbcTile2Bpp> upper = grid.Tiles.Take(imageStartLine * grid.WidthInTiles).ToArray();
        IReadOnlyList<GbcTile2Bpp> lower = grid.Tiles.Skip((imageStartLine + GameBoyCameraConstants.RawPhotoTileHeight) * grid.WidthInTiles).ToArray();

        IReadOnlyList<IReadOnlyList<GbcTile2Bpp>> left = Enumerable.Range(0, GameBoyCameraConstants.RawPhotoTileHeight)
            .Select(line => (IReadOnlyList<GbcTile2Bpp>)grid.Tiles.Skip((line + imageStartLine) * grid.WidthInTiles).Take(2).ToArray())
            .ToArray();

        IReadOnlyList<IReadOnlyList<GbcTile2Bpp>> right = Enumerable.Range(0, GameBoyCameraConstants.RawPhotoTileHeight)
            .Select(line => (IReadOnlyList<GbcTile2Bpp>)grid.Tiles.Skip(((line + imageStartLine) * grid.WidthInTiles) + 18).Take(2).ToArray())
            .ToArray();

        return new GbcFrameOverlay(upper, left, right, lower);
    }
}