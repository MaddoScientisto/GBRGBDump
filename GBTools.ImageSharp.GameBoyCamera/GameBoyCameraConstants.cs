namespace GBTools.ImageSharp.GameBoyCamera;

public static class GameBoyCameraConstants
{
    public const string FormatName = "Game Boy Camera Image";
    public const string DefaultMimeType = "image/x-gameboy-camera";
    public const string DefaultFileExtension = "gbci";
    public const int TileByteCount = 16;
    public const int TilePixelWidth = 8;
    public const int TilePixelHeight = 8;
    public const int RawPhotoTileWidth = 16;
    public const int RawPhotoTileHeight = 14;
    public const int FramedPhotoTileWidth = 20;
    public const int FramedPhotoTileHeight = 18;

    public static ReadOnlySpan<byte> CanonicalMagicHeader => "GBCIMG"u8;
}