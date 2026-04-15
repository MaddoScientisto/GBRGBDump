using GBTools.ImageSharp.GameBoyCamera.Codec;
using GBTools.ImageSharp.GameBoyCamera.Metadata;

namespace GBTools.ImageSharp.GameBoyCamera.Tests.Codec;

public class GameBoyCameraMetadataCodecTests
{
    [Fact]
    public void DecodeText_uses_full_international_char_map()
    {
        byte[] data = [0x56, 0x74, 0xA4, 0xC8, 0x87];

        string result = GameBoyCameraMetadataCodec.DecodeText(data, cartIsJapanese: false);

        Assert.Equal("A\u00c1\ud83d\udcf1@", result);
    }

    [Fact]
    public void DecodeText_uses_full_japanese_char_map()
    {
        byte[] data = [0x01, 0x56, 0x88, 0xF5, 0x87];

        string result = GameBoyCameraMetadataCodec.DecodeText(data, cartIsJapanese: true);

        Assert.Equal("\u3042\u30a2\u30ac@", result);
    }

    [Fact]
    public void DecodeBirthDate_formats_international_and_japanese_dates()
    {
        byte[] birthDate = [0x2A, 0xAA, 0x23, 0x42];

        Assert.Equal("12/31/1999", GameBoyCameraMetadataCodec.DecodeBirthDate(birthDate, cartIsJapanese: false));
        Assert.Equal("1999\u5e7412\u670831\u65e5", GameBoyCameraMetadataCodec.DecodeBirthDate(birthDate, cartIsJapanese: true));
    }

    [Fact]
    public void ParseBasicMetadata_decodes_stock_save_fields()
    {
        byte[] data = new byte[0x1000];
        int baseAddress = 0;

        data[0x0F00] = 0x23;
        data[0x0F01] = 0x45;
        data[0x0F02] = 0x67;
        data[0x0F03] = 0x89;

        data[0x0F04] = 0x56;
        data[0x0F05] = 0x57;
        data[0x0F06] = 0x58;
        data[0x0F07] = 0x87;

        data[0x0F0D] = 0x05;

        data[0x0F0E] = 0x2A;
        data[0x0F0F] = 0xAA;
        data[0x0F10] = 0x23;
        data[0x0F11] = 0x42;

        data[0x0F15] = 0x56;
        data[0x0F16] = 0x57;
        data[0x0F17] = 0x83;
        data[0x0F18] = 0xBA;
        data[0x0F19] = 0xBB;
        data[0x0F33] = 0x01;

        GameBoyCameraBasicMetadata metadata = GameBoyCameraMetadataCodec.ParseBasicMetadata(data, baseAddress, cartIsJapanese: false);

        Assert.Equal("GC-12345678", metadata.UserId);
        Assert.Equal("ABC", metadata.UserName);
        Assert.Equal("12/31/1999", metadata.BirthDate);
        Assert.Equal("m", metadata.Gender);
        Assert.Equal("A", metadata.BloodType);
        Assert.Equal("AB-01", metadata.Comment);
        Assert.True(metadata.IsCopy);
    }

    [Fact]
    public void DetectRomType_distinguishes_stock_pxlr_and_photo_thumbnails()
    {
        byte[] stock = new byte[0x100];
        byte[] pxlr = Enumerable.Repeat((byte)0x00, 0x100).ToArray();
        foreach (int index in Enumerable.Range(0xC8, 8).Concat(Enumerable.Range(0xD8, 8)).Concat(Enumerable.Range(0xE8, 8)).Concat(Enumerable.Range(0xF8, 8)))
        {
            pxlr[index] = 0xFF;
        }

        byte[] photo = new byte[0x100];
        photo[0xC8] = 0x01;

        Assert.Equal(GameBoyCameraRomType.Stock, GameBoyCameraMetadataCodec.DetectRomType(stock));
        Assert.Equal(GameBoyCameraRomType.Pxlr, GameBoyCameraMetadataCodec.DetectRomType(pxlr));
        Assert.Equal(GameBoyCameraRomType.Photo, GameBoyCameraMetadataCodec.DetectRomType(photo));
    }

    [Fact]
    public void ParseCustomMetadata_decodes_photo_metadata()
    {
        byte[] thumbnail = new byte[0x100];
        thumbnail[0xCB] = 0x02;
        thumbnail[0xCA] = 0x01;
        thumbnail[0xC8] = 0b00000010;
        thumbnail[0xC9] = 0b11011111;
        thumbnail[0xCC] = 0b01111101;
        thumbnail[0xCD] = 0b10111111;

        GameBoyCameraCustomMetadata? metadata = GameBoyCameraMetadataCodec.ParseCustomMetadata(thumbnail, GameBoyCameraRomType.Photo);

        Assert.NotNull(metadata);
        Assert.Equal(GameBoyCameraRomType.Photo, metadata!.RomType);
        Assert.Equal("4.1ms", metadata.Exposure);
        Assert.Equal("positive", metadata.CaptureMode);
        Assert.Equal("on", metadata.EdgeExclusive);
        Assert.Equal("vertical", metadata.EdgeOperation);
        Assert.Equal("57.5", metadata.Gain);
        Assert.Equal("500%", metadata.EdgeMode);
        Assert.Equal("on", metadata.InvertOutput);
        Assert.Equal("2.5V", metadata.VoltageReference);
        Assert.Equal("positive", metadata.ZeroPoint);
        Assert.Equal("0.992mV", metadata.VoltageOutput);
        Assert.Null(metadata.DitherSet);
        Assert.Null(metadata.Contrast);
    }

    [Fact]
    public void ParseCustomMetadata_decodes_pxlr_metadata()
    {
        byte[] thumbnail = new byte[0x100];
        thumbnail[0x20] = 0x03;
        thumbnail[0x30] = 0x02;
        thumbnail[0x00] = 0b00000000;
        thumbnail[0x10] = 0b00011000;
        thumbnail[0xC6] = 0b00101010;
        thumbnail[0xD6] = 0b01111110;
        thumbnail[0xE6] = 0b00000011;
        thumbnail[0xF6] = 0x55;

        GameBoyCameraCustomMetadata? metadata = GameBoyCameraMetadataCodec.ParseCustomMetadata(thumbnail, GameBoyCameraRomType.Pxlr);

        Assert.NotNull(metadata);
        Assert.Equal(GameBoyCameraRomType.Pxlr, metadata!.RomType);
        Assert.Equal("8.2ms", metadata.Exposure);
        Assert.Equal("negative", metadata.CaptureMode);
        Assert.Null(metadata.EdgeExclusive);
        Assert.Null(metadata.EdgeOperation);
        Assert.Equal("32.0 (d)", metadata.Gain);
        Assert.Equal("100%", metadata.EdgeMode);
        Assert.Equal("on", metadata.InvertOutput);
        Assert.Equal("1.0V", metadata.VoltageReference);
        Assert.Equal("negative", metadata.ZeroPoint);
        Assert.Equal("0.960mV", metadata.VoltageOutput);
        Assert.Equal("Low/Off", metadata.DitherSet);
        Assert.Equal(0x55, metadata.Contrast);
    }
}