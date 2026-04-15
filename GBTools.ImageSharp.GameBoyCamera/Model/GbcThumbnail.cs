namespace GBTools.ImageSharp.GameBoyCamera.Model;

public sealed class GbcThumbnail
{
    public const int ByteLength = 0x100;

    public GbcThumbnail(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.Length != ByteLength)
        {
            throw new ArgumentException($"A Game Boy Camera thumbnail must be {ByteLength} bytes.", nameof(bytes));
        }

        Bytes = bytes;
    }

    public ReadOnlyMemory<byte> Bytes { get; }
}