using System;
using System.Text;

namespace GBTools.GBxCart.Serial;

public sealed record GbxCartCartridgeInfo(
    string Title,
    byte CartridgeTypeCode,
    byte RomSizeCode,
    byte RamSizeCode,
    int RomSizeBytes,
    int RamSizeBytes)
{
    public bool IsGameBoyCamera => string.Equals(Title, "PHOTO", StringComparison.OrdinalIgnoreCase);

    public static GbxCartCartridgeInfo FromFields(string title, byte cartridgeTypeCode, byte romSizeCode, byte ramSizeCode)
    {
        return new GbxCartCartridgeInfo(
            title.TrimEnd('\0', ' ', (char)0x80).Trim(),
            cartridgeTypeCode,
            romSizeCode,
            ramSizeCode,
            GetRomSizeBytes(romSizeCode),
            GetRamSizeBytes(ramSizeCode));
    }

    public static GbxCartCartridgeInfo FromHeader(byte[] header)
    {
        if (header is null)
        {
            throw new ArgumentNullException(nameof(header));
        }

        if (header.Length < GbxCartProtocolConstants.HeaderLength)
        {
            throw new ArgumentException("The header payload is too short.", nameof(header));
        }

        string title = Encoding.ASCII.GetString(header, GbxCartProtocolConstants.TitleAddress - GbxCartProtocolConstants.HeaderAddress, GbxCartProtocolConstants.TitleLength)
            .TrimEnd('\0', ' ', (char)0x80)
            .Trim();

        byte cartridgeType = header[0x47];
        byte romSizeCode = header[0x48];
        byte ramSizeCode = header[0x49];
        return FromFields(title, cartridgeType, romSizeCode, ramSizeCode);
    }

    private static int GetRomSizeBytes(byte code) => code switch
    {
        0x00 => 32 * 1024,
        0x01 => 64 * 1024,
        0x02 => 128 * 1024,
        0x03 => 256 * 1024,
        0x04 => 512 * 1024,
        0x05 => 1024 * 1024,
        0x06 => 2 * 1024 * 1024,
        0x07 => 4 * 1024 * 1024,
        0x08 => 8 * 1024 * 1024,
        0x52 => 1152 * 1024,
        0x53 => 1280 * 1024,
        0x54 => 1536 * 1024,
        _ => 0,
    };

    private static int GetRamSizeBytes(byte code) => code switch
    {
        0x00 => 0,
        0x01 => 2 * 1024,
        0x02 => 8 * 1024,
        0x03 => 32 * 1024,
        0x04 => 128 * 1024,
        0x05 => 64 * 1024,
        _ => 0,
    };
}