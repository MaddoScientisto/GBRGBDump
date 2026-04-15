using System.Globalization;
using GBTools.ImageSharp.GameBoyCamera.Model;

namespace GBTools.ImageSharp.GameBoyCamera.Codec;

public static class GameBoyCameraTileTextCodec
{
    public static bool TryParseTile(string rawText, out GbcTile2Bpp? tile)
    {
        tile = null;
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return false;
        }

        string hex = new(rawText.Where(Uri.IsHexDigit).ToArray());
        if (hex.Length != GameBoyCameraConstants.TileByteCount * 2)
        {
            return false;
        }

        byte[] bytes = GC.AllocateUninitializedArray<byte>(GameBoyCameraConstants.TileByteCount);
        for (int index = 0; index < bytes.Length; index++)
        {
            if (!byte.TryParse(hex.AsSpan(index * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out bytes[index]))
            {
                return false;
            }
        }

        tile = new GbcTile2Bpp(bytes);
        return true;
    }

    public static GbcTile2Bpp ParseTile(string rawText)
    {
        if (!TryParseTile(rawText, out GbcTile2Bpp? tile) || tile is null)
        {
            throw new FormatException("Expected a Game Boy tile encoded as 16 bytes written as 32 hex characters.");
        }

        return tile;
    }

    public static string FormatTile(GbcTile2Bpp tile)
    {
        ArgumentNullException.ThrowIfNull(tile);

        return string.Join(" ", tile.Bytes.Span.ToArray().Select(static value => value.ToString("X2", CultureInfo.InvariantCulture)));
    }

    public static IReadOnlyList<GbcTile2Bpp> ParseTiles(IEnumerable<string> rawTiles)
    {
        ArgumentNullException.ThrowIfNull(rawTiles);
        return rawTiles.Select(ParseTile).ToArray();
    }
}