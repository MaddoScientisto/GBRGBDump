using GBTools.ImageSharp.GameBoyCamera.Model;

namespace GBTools.ImageSharp.GameBoyCamera.Codec;

public static class GameBoyCameraRawPhotoTransform
{
    private const int RawPhotoByteLength = 0x0E00;

    public static readonly GbcTile2Bpp BlackTile = GameBoyCameraTileTextCodec.ParseTile("FF FF FF FF FF FF FF FF FF FF FF FF FF FF FF FF");
    public static readonly GbcTile2Bpp WhiteTile = GameBoyCameraTileTextCodec.ParseTile("00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00");

    public static bool TryTransformFramedPhoto(ReadOnlySpan<byte> data, int baseAddress, out GbcTileGrid? tileGrid)
    {
        tileGrid = null;

        if (baseAddress < 0 || baseAddress + RawPhotoByteLength > data.Length)
        {
            return false;
        }

        List<GbcTile2Bpp> tiles = new(GameBoyCameraConstants.FramedPhotoTileWidth * GameBoyCameraConstants.FramedPhotoTileHeight);
        bool hasData = false;

        for (int i = 0; i < 40; i++)
        {
            tiles.Add(BlackTile);
        }

        for (int offset = 0; offset < RawPhotoByteLength; offset += GameBoyCameraConstants.TileByteCount)
        {
            if (offset % 256 == 0)
            {
                tiles.Add(BlackTile);
                tiles.Add(BlackTile);
            }

            GbcTile2Bpp tile = new(data.Slice(baseAddress + offset, GameBoyCameraConstants.TileByteCount).ToArray());
            tiles.Add(tile);

            if (!hasData && !tile.Bytes.Span.SequenceEqual(WhiteTile.Bytes.Span) && !tile.Bytes.Span.SequenceEqual(BlackTile.Bytes.Span))
            {
                hasData = true;
            }

            if ((offset % 256) == 240)
            {
                tiles.Add(BlackTile);
                tiles.Add(BlackTile);
            }
        }

        for (int i = 0; i < 40; i++)
        {
            tiles.Add(BlackTile);
        }

        if (!hasData)
        {
            return false;
        }

        tileGrid = new GbcTileGrid(GameBoyCameraConstants.FramedPhotoTileWidth, GameBoyCameraConstants.FramedPhotoTileHeight, tiles);
        return true;
    }
}