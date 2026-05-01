using System;

namespace GBTools.GBxCart.Serial;

public static class GbxCartProtocolConstants
{
    public const int DefaultBaudRate = 1_000_000;
    public const int MaxTransferSize = 64;
    public const int HeaderAddress = 0x100;
    public const int HeaderLength = 0x50;
    public const int TitleAddress = 0x134;
    public const int TitleLength = 0x10;
    public const int GameBoyCameraRomSizeBytes = 0x100000;
    public const int GameBoyCameraRomBankSize = 0x4000;
    public const int GameBoyCameraRomBankCount = GameBoyCameraRomSizeBytes / GameBoyCameraRomBankSize;
    public const int GameBoyCameraSaveBankSize = 0x2000;
    public const int GameBoyCameraSaveBankCount = 16;
    public const int GameBoyCameraSaveSizeBytes = GameBoyCameraSaveBankSize * GameBoyCameraSaveBankCount;
}