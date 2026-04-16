namespace GBTools.PicNRec.Serial;

public sealed class PicNRecClientOptions
{
    public string PortName { get; set; } = string.Empty;

    public bool ConnectInFastMode { get; set; }

    public bool EnableDtr { get; set; } = true;

    public bool EnableRts { get; set; } = true;

    public int ReadBufferSize { get; set; } = 4096;

    public int BlockTimeoutMs { get; set; } = PicNRecProtocolConstants.DefaultBlockTimeoutMs;

    public int FlushSilenceMs { get; set; } = 40;

    public int FlushMaxDrainMs { get; set; } = 300;

    public int FastModeSettleDelayMs { get; set; } = 100;

    public int MetadataReadRetryCount { get; set; } = 2;

    public int MetadataReadRetryDelayMs { get; set; } = PicNRecProtocolConstants.DefaultRetryDelayMs;

    public int ImageReadRetryCount { get; set; } = PicNRecProtocolConstants.DefaultRetryCount;

    public int ImageReadRetryDelayMs { get; set; } = PicNRecProtocolConstants.DefaultRetryDelayMs;

    public int PortReadTimeoutMs { get; set; } = 50;

    public int PortWriteTimeoutMs { get; set; } = 1000;
}