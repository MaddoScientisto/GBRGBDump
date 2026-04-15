using GBTools.ImageSharp.GameBoyCamera.Model;

namespace GBTools.ImageSharp.GameBoyCamera.Codec;

public static class GameBoyCameraTileGridFactory
{
    public static GbcTileGrid CreateFromTextTiles(IEnumerable<string> rawTiles, int widthInTiles)
    {
        ArgumentNullException.ThrowIfNull(rawTiles);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(widthInTiles);

        GbcTile2Bpp[] tiles = GameBoyCameraTileTextCodec.ParseTiles(rawTiles).ToArray();
        if (tiles.Length % widthInTiles != 0)
        {
            throw new ArgumentException("Tile count must divide evenly by the grid width.", nameof(rawTiles));
        }

        return new GbcTileGrid(widthInTiles, tiles.Length / widthInTiles, tiles);
    }

    public static GbcTileGrid CreateFromBinary(ReadOnlySpan<byte> data, int widthInTiles)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(widthInTiles);

        if (data.Length % GameBoyCameraConstants.TileByteCount != 0)
        {
            throw new ArgumentException("Binary tile payload length must be divisible by 16.", nameof(data));
        }

        int tileCount = data.Length / GameBoyCameraConstants.TileByteCount;
        if (tileCount % widthInTiles != 0)
        {
            throw new ArgumentException("Tile count must divide evenly by the grid width.", nameof(data));
        }

        GbcTile2Bpp[] tiles = new GbcTile2Bpp[tileCount];
        for (int index = 0; index < tileCount; index++)
        {
            tiles[index] = new GbcTile2Bpp(data.Slice(index * GameBoyCameraConstants.TileByteCount, GameBoyCameraConstants.TileByteCount).ToArray());
        }

        return new GbcTileGrid(widthInTiles, tileCount / widthInTiles, tiles);
    }
}