namespace GBTools.ImageSharp.GameBoyCamera.Model;

public sealed class GbcTile2Bpp
{
    public GbcTile2Bpp(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.Length != GameBoyCameraConstants.TileByteCount)
        {
            throw new ArgumentException($"A Game Boy 2bpp tile must be {GameBoyCameraConstants.TileByteCount} bytes.", nameof(bytes));
        }

        Bytes = bytes;
    }

    public ReadOnlyMemory<byte> Bytes { get; }
}