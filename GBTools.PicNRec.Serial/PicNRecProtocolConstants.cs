namespace GBTools.PicNRec.Serial;

public static class PicNRecProtocolConstants
{
    public const int DefaultBaudRate = 1_000_000;
    public const int FastBaudRate = 1_700_000;
    public const int BlockSize = 64;
    public const int ImageSize = 3_584;
    public const int ImageBlockCount = ImageSize / BlockSize;
    public const int MetadataSize = 2_560;
    public const int MetadataBlockCount = MetadataSize / BlockSize;
    public const int LastImageScanBytes = 2_500;
    public const int DefaultBlockTimeoutMs = 500;
    public const int DefaultRetryCount = 3;
    public const int DefaultRetryDelayMs = 200;
}