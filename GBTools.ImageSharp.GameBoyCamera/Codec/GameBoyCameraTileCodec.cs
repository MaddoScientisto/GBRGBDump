using GBTools.ImageSharp.GameBoyCamera.Model;

namespace GBTools.ImageSharp.GameBoyCamera.Codec;

public static class GameBoyCameraTileCodec
{
    public static byte[] DecodeToColorIndexes(GbcTile2Bpp tile)
    {
        ArgumentNullException.ThrowIfNull(tile);

        byte[] pixels = GC.AllocateUninitializedArray<byte>(GameBoyCameraConstants.TilePixelWidth * GameBoyCameraConstants.TilePixelHeight);
        ReadOnlySpan<byte> source = tile.Bytes.Span;

        for (int y = 0; y < GameBoyCameraConstants.TilePixelHeight; y++)
        {
            byte low = source[y * 2];
            byte high = source[(y * 2) + 1];

            for (int x = 0; x < GameBoyCameraConstants.TilePixelWidth; x++)
            {
                int shift = 7 - x;
                int hiBit = (high >> shift) & 0x01;
                int loBit = (low >> shift) & 0x01;
                pixels[(y * GameBoyCameraConstants.TilePixelWidth) + x] = (byte)((hiBit << 1) | loBit);
            }
        }

        return pixels;
    }

    public static GbcTile2Bpp EncodeFromColorIndexes(ReadOnlySpan<byte> pixels)
    {
        if (pixels.Length != GameBoyCameraConstants.TilePixelWidth * GameBoyCameraConstants.TilePixelHeight)
        {
            throw new ArgumentException("A Game Boy tile requires exactly 64 palette index values.", nameof(pixels));
        }

        byte[] bytes = GC.AllocateUninitializedArray<byte>(GameBoyCameraConstants.TileByteCount);

        for (int y = 0; y < GameBoyCameraConstants.TilePixelHeight; y++)
        {
            byte low = 0;
            byte high = 0;

            for (int x = 0; x < GameBoyCameraConstants.TilePixelWidth; x++)
            {
                byte value = pixels[(y * GameBoyCameraConstants.TilePixelWidth) + x];
                if (value > 3)
                {
                    throw new ArgumentOutOfRangeException(nameof(pixels), "Palette indexes must be in the inclusive range 0-3.");
                }

                int shift = 7 - x;
                low |= (byte)((value & 0x01) << shift);
                high |= (byte)(((value >> 1) & 0x01) << shift);
            }

            bytes[y * 2] = low;
            bytes[(y * 2) + 1] = high;
        }

        return new GbcTile2Bpp(bytes);
    }
}