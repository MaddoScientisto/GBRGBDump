namespace GBTools.PicoGbPrinter.Serial;

public sealed class PicoGbPrinterPortInfo
{
    public PicoGbPrinterPortInfo(string portName, bool respondedToProbe)
    {
        PortName = portName;
        RespondedToProbe = respondedToProbe;
    }

    public string PortName { get; }

    public bool RespondedToProbe { get; }
}
