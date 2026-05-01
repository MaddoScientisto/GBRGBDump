using System;

namespace GBTools.GBxCart.Serial;

public static class GbxCartProtocolConstants
{
    public const int DefaultBaudRate = 1_000_000;
    public const int LegacyTransferSize = 0x800;
    public const int LatestFirmwareTransferSize = 0x1000;
    public const int MaxTransferSize = LatestFirmwareTransferSize;
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
    public const int GameBoyCameraPhotoAlbumFirstRomBank = 8;
    public const int GameBoyCameraPhotoAlbumRomBankCount = GameBoyCameraRomBankCount - GameBoyCameraPhotoAlbumFirstRomBank;
    public const int GameBoyCameraPhotoAlbumSizeBytes = GameBoyCameraSaveSizeBytes + (GameBoyCameraPhotoAlbumRomBankCount * GameBoyCameraRomBankSize);
}