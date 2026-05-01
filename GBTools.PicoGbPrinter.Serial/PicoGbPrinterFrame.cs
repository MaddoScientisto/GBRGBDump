namespace GBTools.PicoGbPrinter.Serial;

internal sealed class PicoGbPrinterFrame
{
    public PicoGbPrinterFrame(byte type, ushort flags, byte[] payload)
    {
        Type = type;
        Flags = flags;
        Payload = payload;
    }

    public byte Type { get; }

    public ushort Flags { get; }

    public byte[] Payload { get; }
}
