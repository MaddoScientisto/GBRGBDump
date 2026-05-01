namespace GBTools.PicoGbPrinter.Serial;

public sealed class PicoGbPrinterCapture
{
    public PicoGbPrinterCapture(byte[] data, ushort flags)
    {
        Data = data;
        Flags = flags;
    }

    public byte[] Data { get; }

    public ushort Flags { get; }

    public bool IsReplay => (Flags & PicoGbPrinterProtocolConstants.ReplayFlag) != 0;

    public bool IsTransfer => (Flags & PicoGbPrinterProtocolConstants.TransferFlag) != 0;
}
