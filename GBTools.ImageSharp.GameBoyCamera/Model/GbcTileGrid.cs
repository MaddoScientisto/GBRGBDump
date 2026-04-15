namespace GBTools.ImageSharp.GameBoyCamera.Model;

public sealed class GbcTileGrid
{
    public GbcTileGrid(int widthInTiles, int heightInTiles, IReadOnlyList<GbcTile2Bpp> tiles)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(widthInTiles);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(heightInTiles);
        ArgumentNullException.ThrowIfNull(tiles);

        int expectedCount = widthInTiles * heightInTiles;
        if (tiles.Count != expectedCount)
        {
            throw new ArgumentException($"Expected {expectedCount} tiles for a {widthInTiles}x{heightInTiles} grid.", nameof(tiles));
        }

        WidthInTiles = widthInTiles;
        HeightInTiles = heightInTiles;
        Tiles = tiles;
    }

    public int WidthInTiles { get; }

    public int HeightInTiles { get; }

    public IReadOnlyList<GbcTile2Bpp> Tiles { get; }
}