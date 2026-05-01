namespace GBTools.PicoGbPrinter.Serial;

public static class PicoGbPrinterProtocolConstants
{
    public const int DefaultBaudRate = 115200;
    public const int DefaultCommandTimeoutMs = 3000;
    public const int DefaultCaptureTimeoutMs = 120000;
    public const int DefaultBannerTimeoutMs = 3000;

    public const string Banner = "PICO GB SERIAL v1";

    public const byte FrameVersion = 1;
    public const int FrameHeaderLength = 12;
    public const int FrameCrcLength = 4;

    public const ushort ReplayFlag = 0x0001;
    public const ushort TransferFlag = 0x0002;

    public const byte HelloFrame = 0x01;
    public const byte StatusFrame = 0x02;
    public const byte JobFrame = 0x10;
    public const byte AckFrame = 0x20;
    public const byte ErrorFrame = 0x7f;
}
