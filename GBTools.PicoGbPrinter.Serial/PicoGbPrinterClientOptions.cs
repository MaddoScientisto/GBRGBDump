using System;

namespace GBTools.PicoGbPrinter.Serial;

public sealed class PicoGbPrinterClientOptions
{
    public string PortName { get; set; } = string.Empty;

    public IProgress<string>? Trace { get; set; }

    public int BaudRate { get; set; } = PicoGbPrinterProtocolConstants.DefaultBaudRate;

    public bool EnableDtr { get; set; } = true;

    public bool EnableRts { get; set; } = true;

    public int ReadBufferSize { get; set; } = 4096;

    public int PortReadTimeoutMs { get; set; } = 50;

    public int PortWriteTimeoutMs { get; set; } = 1000;

    public int CommandTimeoutMs { get; set; } = PicoGbPrinterProtocolConstants.DefaultCommandTimeoutMs;

    public int CaptureTimeoutMs { get; set; } = PicoGbPrinterProtocolConstants.DefaultCaptureTimeoutMs;

    public int CaptureIdleTimeoutMs { get; set; } = 250;

    public int BannerTimeoutMs { get; set; } = PicoGbPrinterProtocolConstants.DefaultBannerTimeoutMs;
}
