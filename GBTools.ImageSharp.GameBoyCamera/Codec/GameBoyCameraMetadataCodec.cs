using System.Globalization;
using GBTools.ImageSharp.GameBoyCamera.Metadata;

namespace GBTools.ImageSharp.GameBoyCamera.Codec;

public static class GameBoyCameraMetadataCodec
{
    private const byte GenderMaleMask = 0x01;
    private const byte GenderFemaleMask = 0x02;
    private const byte BloodTypeMask = 0x1C;
    private const byte CaptureMask = 0b00000010;
    private const byte EdgeExclusiveMask = 0b10000000;
    private const byte EdgeOperationMask = 0b01100000;
    private const byte GainMask = 0b00011111;
    private const byte EdgeRatioMask = 0b01110000;
    private const byte InvertOutputMask = 0b00001000;
    private const byte VoltageReferenceMask = 0b00000111;
    private const byte ZeroPointMask = 0b11000000;
    private const byte VoltageOutputMask = 0b00111111;
    private const byte DitherSetMask = 0b00000001;
    private const byte DitherOnOffMask = 0b00000010;

    private static readonly string[] InternationalCharacterMap = CreateInternationalCharacterMap();
    private static readonly string[] JapaneseCharacterMap = CreateJapaneseCharacterMap();
    private static readonly char[] DateDigitMap = "00123456789?????".ToCharArray();

    public static string DecodeText(ReadOnlySpan<byte> data, bool cartIsJapanese)
    {
        string[] map = cartIsJapanese ? JapaneseCharacterMap : InternationalCharacterMap;
        return string.Concat(data.ToArray().Select(value => map[value] ?? " ")).Trim();
    }

    public static string DecodeBirthDate(ReadOnlySpan<byte> birthDate, bool cartIsJapanese)
    {
        if (birthDate.Length < 4)
        {
            throw new ArgumentException("Birth date data must contain four bytes.", nameof(birthDate));
        }

        string[] parts = birthDate[..4].ToArray().Select(static value => ConvertDigit(value)).ToArray();
        string fullYear = ConcatYear(parts[0], parts[1]);

        return cartIsJapanese
            ? $"{fullYear}\u5e74{parts[2]}\u6708{parts[3]}\u65e5"
            : $"{parts[2]}/{parts[3]}/{fullYear}";
    }

    public static string DecodeUserId(ReadOnlySpan<byte> userId, bool cartIsJapanese)
    {
        if (userId.Length < 4)
        {
            throw new ArgumentException("User ID data must contain four bytes.", nameof(userId));
        }

        string digits = string.Concat(userId[..4].ToArray().Select(static value => ConvertDigit(value)).Select(static value => value == "--" ? "00" : value));
        return $"{(cartIsJapanese ? "PC-" : "GC-")}{digits}";
    }

    public static string ParseGender(byte value)
    {
        if ((value & GenderMaleMask) != 0)
        {
            return "m";
        }

        if ((value & GenderFemaleMask) != 0)
        {
            return "f";
        }

        return "-";
    }

    public static string ParseBloodType(byte value) => (value & BloodTypeMask) switch
    {
        0x04 => "A",
        0x08 => "B",
        0x0C => "0",
        0x10 => "AB",
        _ => "-",
    };

    public static GameBoyCameraBasicMetadata ParseBasicMetadata(ReadOnlySpan<byte> data, int baseAddress, bool cartIsJapanese)
    {
        if (baseAddress < 0 || baseAddress + 0x0F33 >= data.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(baseAddress));
        }

        byte genderAndBloodType = data[baseAddress + 0x0F0D];
        return new GameBoyCameraBasicMetadata(
            DecodeUserId(data.Slice(baseAddress + 0x0F00, 4), cartIsJapanese),
            DecodeText(data.Slice(baseAddress + 0x0F04, 9), cartIsJapanese),
            DecodeBirthDate(data.Slice(baseAddress + 0x0F0E, 4), cartIsJapanese),
            ParseGender(genderAndBloodType),
            ParseBloodType(genderAndBloodType),
            DecodeText(data.Slice(baseAddress + 0x0F15, 27), cartIsJapanese),
            data[baseAddress + 0x0F33] != 0);
    }

    public static GameBoyCameraRomType DetectRomType(ReadOnlySpan<byte> thumbnail)
    {
        if (thumbnail.Length < 0x100)
        {
            throw new ArgumentException("Thumbnail data must contain 256 bytes.", nameof(thumbnail));
        }

        ReadOnlySpan<int> unusedLines = [
            0xC8, 0xC9, 0xCA, 0xCB, 0xCC, 0xCD, 0xCE, 0xCF,
            0xD8, 0xD9, 0xDA, 0xDB, 0xDC, 0xDD, 0xDE, 0xDF,
            0xE8, 0xE9, 0xEA, 0xEB, 0xEC, 0xED, 0xEE, 0xEF,
            0xF8, 0xF9, 0xFA, 0xFB, 0xFC, 0xFD, 0xFE, 0xFF,
        ];

        bool allZero = true;
        bool allFf = true;
        foreach (int index in unusedLines)
        {
            byte value = thumbnail[index];
            allZero &= value == 0x00;
            allFf &= value == 0xFF;
        }

        if (allZero)
        {
            return GameBoyCameraRomType.Stock;
        }

        if (allFf)
        {
            return GameBoyCameraRomType.Pxlr;
        }

        return GameBoyCameraRomType.Photo;
    }

    public static GameBoyCameraCustomMetadata? ParseCustomMetadata(ReadOnlySpan<byte> thumbnail, GameBoyCameraRomType romType)
    {
        if (thumbnail.Length < 0x100)
        {
            throw new ArgumentException("Thumbnail data must contain 256 bytes.", nameof(thumbnail));
        }

        ThumbnailOffsets? offsets = romType switch
        {
            GameBoyCameraRomType.Photo => new ThumbnailOffsets(0xC8, 0xC9, 0xCB, 0xCA, 0xCC, 0xCD, 0xC8, 0xC8),
            GameBoyCameraRomType.Pxlr => new ThumbnailOffsets(0x00, 0x10, 0x20, 0x30, 0xC6, 0xD6, 0xE6, 0xF6),
            _ => null,
        };

        if (offsets is null)
        {
            return null;
        }

        byte exposureHigh = thumbnail[offsets.Value.ExposureHigh];
        byte exposureLow = thumbnail[offsets.Value.ExposureLow];
        byte captureMode = (byte)(thumbnail[offsets.Value.Capture] & CaptureMask);
        byte edgeExclusive = (byte)(thumbnail[offsets.Value.EdgeGains] & EdgeExclusiveMask);
        byte edgeOperation = (byte)(thumbnail[offsets.Value.EdgeGains] & EdgeOperationMask);
        byte gain = (byte)(thumbnail[offsets.Value.EdgeGains] & GainMask);
        byte edgeMode = (byte)(thumbnail[offsets.Value.EdgeModeVoltage] & EdgeRatioMask);
        byte invertOutput = (byte)(thumbnail[offsets.Value.EdgeModeVoltage] & InvertOutputMask);
        byte voltageReference = (byte)(thumbnail[offsets.Value.EdgeModeVoltage] & VoltageReferenceMask);
        byte zeroPoint = (byte)(thumbnail[offsets.Value.VoltageOutputZero] & ZeroPointMask);
        byte voltageOutput = (byte)(thumbnail[offsets.Value.VoltageOutputZero] & VoltageOutputMask);
        byte ditherSet = thumbnail[offsets.Value.DitherSet];
        int contrast = thumbnail[offsets.Value.Contrast];

        return new GameBoyCameraCustomMetadata(
            romType,
            GetExposureTime(exposureHigh, exposureLow),
            GetCaptureMode(captureMode),
            romType == GameBoyCameraRomType.Photo ? GetEdgeExclusive(edgeExclusive) : null,
            romType == GameBoyCameraRomType.Photo ? GetEdgeOperation(edgeOperation) : null,
            GetGain(gain),
            GetEdgeMode(edgeMode),
            GetInvertOutput(invertOutput),
            GetVoltageReference(voltageReference),
            GetZeroPoint(zeroPoint),
            GetVoltageOutput(voltageOutput),
            romType == GameBoyCameraRomType.Pxlr ? GetDitherSet(ditherSet) : null,
            romType == GameBoyCameraRomType.Pxlr ? contrast : null);
    }

    private static string ConvertDigit(byte byteValue)
    {
        if (byteValue == 0)
        {
            return "--";
        }

        char upperFormat = DateDigitMap[byteValue >> 4];
        char lowerFormat = DateDigitMap[byteValue & 0b00001111];
        return string.Create(2, (upperFormat, lowerFormat), static (buffer, state) =>
        {
            buffer[0] = state.upperFormat;
            buffer[1] = state.lowerFormat;
        });
    }

    private static string ConcatYear(string year1, string year2)
    {
        if (year1 == "--" && year2 != "--")
        {
            return $"00{year2}";
        }

        if (year1 != "--" && year2 == "--")
        {
            return $"{year1}00";
        }

        return $"{year1}{year2}";
    }

    private static string GetExposureTime(byte exposureHigh, byte exposureLow)
    {
        double timeMs = (0.016 * exposureHigh) + (4.096 * exposureLow);
        return timeMs < 10
            ? string.Create(CultureInfo.InvariantCulture, $"{timeMs:F1}ms")
            : string.Create(CultureInfo.InvariantCulture, $"{Math.Floor(timeMs):F0}ms");
    }

    private static string GetCaptureMode(byte captureMode) => captureMode switch
    {
        0b00000010 => "positive",
        0b00000000 => "negative",
        _ => "unknown",
    };

    private static string GetEdgeExclusive(byte edgeExclusive) => edgeExclusive switch
    {
        0b10000000 => "on",
        0b00000000 => "off",
        _ => "unknown",
    };

    private static string GetEdgeOperation(byte edgeOperation) => edgeOperation switch
    {
        0b00000000 => "none",
        0b00100000 => "horizontal",
        0b01000000 => "vertical",
        0b01100000 => "2d",
        _ => "unknown",
    };

    private static string GetGain(byte gain) => gain switch
    {
        0b00000000 => "14.0",
        0b00000001 => "15.5",
        0b00000010 => "17.0",
        0b00000011 => "18.5",
        0b00000100 => "20.0",
        0b00010000 => "20.0 (d)",
        0b00000101 => "21.5",
        0b00010001 => "21.5 (d)",
        0b00000110 => "23.0",
        0b00010010 => "23.0 (d)",
        0b00000111 => "24.5",
        0b00010011 => "24.5 (d)",
        0b00001000 => "26.0",
        0b00010100 => "26.0 (d)",
        0b00010101 => "27.5",
        0b00001001 => "29.0",
        0b00010110 => "29.0 (d)",
        0b00010111 => "30.5",
        0b00001010 => "32.0",
        0b00011000 => "32.0 (d)",
        0b00001011 => "35.0",
        0b00011001 => "35.0 (d)",
        0b00001100 => "38.0",
        0b00011010 => "38.0 (d)",
        0b00001101 => "41.0",
        0b00011011 => "41.0 (d)",
        0b00011100 => "44.0",
        0b00001110 => "45.5",
        0b00011101 => "47.0",
        0b00001111 => "51.5",
        0b00011110 => "51.5 (d)",
        0b00011111 => "57.5",
        _ => "unknown",
    };

    private static string GetEdgeMode(byte edgeMode) => edgeMode switch
    {
        0b00000000 => "50%",
        0b00010000 => "75%",
        0b00100000 => "100%",
        0b00110000 => "125%",
        0b01000000 => "200%",
        0b01010000 => "300%",
        0b01100000 => "400%",
        0b01110000 => "500%",
        _ => "unknown",
    };

    private static string GetInvertOutput(byte invertOutput) => invertOutput switch
    {
        0b00001000 => "on",
        0b00000000 => "off",
        _ => "unknown",
    };

    private static string GetVoltageReference(byte voltageReference) => voltageReference switch
    {
        0b00000000 => "0.0V",
        0b00000001 => "0.5V",
        0b00000010 => "1.0V",
        0b00000011 => "1.5V",
        0b00000100 => "2.0V",
        0b00000101 => "2.5V",
        0b00000110 => "3.0V",
        0b00000111 => "3.5V",
        _ => "unknown",
    };

    private static string GetZeroPoint(byte zeroPoint) => zeroPoint switch
    {
        0b00000000 => "none",
        0b10000000 => "positive",
        0b01000000 => "negative",
        _ => "unknown",
    };

    private static string GetVoltageOutput(byte voltageOutput) => voltageOutput switch
    {
        0b00011111 => "-0.992mV",
        0b00011110 => "-0.960mV",
        0b00011101 => "-0.928mV",
        0b00011100 => "-0.896mV",
        0b00011011 => "-0.864mV",
        0b00011010 => "-0.832mV",
        0b00011001 => "-0.800mV",
        0b00011000 => "-0.768mV",
        0b00010111 => "-0.736mV",
        0b00010110 => "-0.704mV",
        0b00010101 => "-0.672mV",
        0b00010100 => "-0.640mV",
        0b00010011 => "-0.608mV",
        0b00010010 => "-0.576mV",
        0b00010001 => "-0.544mV",
        0b00010000 => "-0.512mV",
        0b00001111 => "-0.480mV",
        0b00001110 => "-0.448mV",
        0b00001101 => "-0.416mV",
        0b00001100 => "-0.384mV",
        0b00001011 => "-0.352mV",
        0b00001010 => "-0.320mV",
        0b00001001 => "-0.288mV",
        0b00001000 => "-0.256mV",
        0b00000111 => "-0.224mV",
        0b00000110 => "-0.192mV",
        0b00000101 => "-0.160mV",
        0b00000100 => "-0.128mV",
        0b00000011 => "-0.096mV",
        0b00000010 => "-0.064mV",
        0b00000001 => "-0.032mV",
        0b00000000 => "-0.000mV",
        0b00100000 => " 0.000mV",
        0b00100001 => "0.032mV",
        0b00100010 => "0.064mV",
        0b00100011 => "0.096mV",
        0b00100100 => "0.128mV",
        0b00100101 => "0.160mV",
        0b00100110 => "0.192mV",
        0b00100111 => "0.224mV",
        0b00101000 => "0.256mV",
        0b00101001 => "0.288mV",
        0b00101010 => "0.320mV",
        0b00101011 => "0.352mV",
        0b00101100 => "0.384mV",
        0b00101101 => "0.416mV",
        0b00101110 => "0.448mV",
        0b00101111 => "0.480mV",
        0b00110000 => "0.512mV",
        0b00110001 => "0.544mV",
        0b00110010 => "0.576mV",
        0b00110011 => "0.608mV",
        0b00110100 => "0.640mV",
        0b00110101 => "0.672mV",
        0b00110110 => "0.704mV",
        0b00110111 => "0.736mV",
        0b00111000 => "0.768mV",
        0b00111001 => "0.800mV",
        0b00111010 => "0.832mV",
        0b00111011 => "0.864mV",
        0b00111100 => "0.896mV",
        0b00111101 => "0.928mV",
        0b00111110 => "0.960mV",
        0b00111111 => "0.992mV",
        _ => "unknown",
    };

    private static string GetDitherSet(byte ditherSet)
    {
        string set = (ditherSet & DitherSetMask) switch
        {
            0b00000000 => "High",
            0b00000001 => "Low",
            _ => "unknown",
        };

        string onOff = (ditherSet & DitherOnOffMask) switch
        {
            0b00000000 => "On",
            0b00000010 => "Off",
            _ => "unknown",
        };

        return $"{set}/{onOff}";
    }

    private static string[] CreateInternationalCharacterMap()
    {
        string[] map = Enumerable.Repeat(" ", 256).ToArray();
        Set(map, 0x56, "A");
        Set(map, 0x57, "B");
        Set(map, 0x58, "C");
        Set(map, 0x59, "D");
        Set(map, 0x5A, "E");
        Set(map, 0x5B, "F");
        Set(map, 0x5C, "G");
        Set(map, 0x5D, "H");
        Set(map, 0x5E, "I");
        Set(map, 0x5F, "J");
        Set(map, 0x60, "K");
        Set(map, 0x61, "L");
        Set(map, 0x62, "M");
        Set(map, 0x63, "N");
        Set(map, 0x64, "O");
        Set(map, 0x65, "P");
        Set(map, 0x66, "Q");
        Set(map, 0x67, "R");
        Set(map, 0x68, "S");
        Set(map, 0x69, "T");
        Set(map, 0x6A, "U");
        Set(map, 0x6B, "V");
        Set(map, 0x6C, "W");
        Set(map, 0x6D, "X");
        Set(map, 0x6E, "Y");
        Set(map, 0x6F, "Z");
        Set(map, 0x70, "_");
        Set(map, 0x71, "'");
        Set(map, 0x72, ",");
        Set(map, 0x73, ".");
        Set(map, 0x74, "\u00c1");
        Set(map, 0x75, "\u00c2");
        Set(map, 0x76, "\u00c0");
        Set(map, 0x77, "\u00c4");
        Set(map, 0x78, "\u00c9");
        Set(map, 0x79, "\u00ca");
        Set(map, 0x7A, "\u00c8");
        Set(map, 0x7B, "\u00cb");
        Set(map, 0x7C, "\u00cd");
        Set(map, 0x7D, "\u00cf");
        Set(map, 0x7E, "\u00d3");
        Set(map, 0x7F, "\u00d6");
        Set(map, 0x80, "\u00da");
        Set(map, 0x81, "\u00dc");
        Set(map, 0x82, "\u00d1");
        Set(map, 0x83, "-");
        Set(map, 0x84, "&");
        Set(map, 0x85, "!");
        Set(map, 0x86, "?");
        Set(map, 0x87, " ");
        Set(map, 0x88, "a");
        Set(map, 0x89, "b");
        Set(map, 0x8A, "c");
        Set(map, 0x8B, "d");
        Set(map, 0x8C, "e");
        Set(map, 0x8D, "f");
        Set(map, 0x8E, "g");
        Set(map, 0x8F, "h");
        Set(map, 0x90, "i");
        Set(map, 0x91, "j");
        Set(map, 0x92, "k");
        Set(map, 0x93, "l");
        Set(map, 0x94, "m");
        Set(map, 0x95, "n");
        Set(map, 0x96, "o");
        Set(map, 0x97, "p");
        Set(map, 0x98, "q");
        Set(map, 0x99, "r");
        Set(map, 0x9A, "s");
        Set(map, 0x9B, "t");
        Set(map, 0x9C, "u");
        Set(map, 0x9D, "v");
        Set(map, 0x9E, "w");
        Set(map, 0x9F, "x");
        Set(map, 0xA0, "y");
        Set(map, 0xA1, "z");
        Set(map, 0xA2, "\u2022");
        Set(map, 0xA3, "~");
        Set(map, 0xA4, "\ud83d\udcf1");
        Set(map, 0xA5, " ");
        Set(map, 0xA6, "\u00e1");
        Set(map, 0xA7, "\u00e2");
        Set(map, 0xA8, "\u00e0");
        Set(map, 0xA9, "\u00e4");
        Set(map, 0xAA, "\u00e9");
        Set(map, 0xAB, "\u00ea");
        Set(map, 0xAC, "\u00e8");
        Set(map, 0xAD, "\u00eb");
        Set(map, 0xAE, "\u00ed");
        Set(map, 0xAF, "\u00ef");
        Set(map, 0xB0, "\u00f3");
        Set(map, 0xB1, "\u00f6");
        Set(map, 0xB2, "\u00fa");
        Set(map, 0xB3, "\u00fc");
        Set(map, 0xB4, "\u00f1");
        Set(map, 0xB5, "\u1e09");
        Set(map, 0xB6, "\u00df");
        Set(map, 0xB7, "\ud83d\ude04");
        Set(map, 0xB8, "\ud83d\ude1f");
        Set(map, 0xB9, "\ud83d\ude05");
        Set(map, 0xBA, "0");
        Set(map, 0xBB, "1");
        Set(map, 0xBC, "2");
        Set(map, 0xBD, "3");
        Set(map, 0xBE, "4");
        Set(map, 0xBF, "5");
        Set(map, 0xC0, "6");
        Set(map, 0xC1, "7");
        Set(map, 0xC2, "8");
        Set(map, 0xC3, "9");
        Set(map, 0xC4, "/");
        Set(map, 0xC5, ":");
        Set(map, 0xC6, "~");
        Set(map, 0xC7, "\"");
        Set(map, 0xC8, "@");
        return map;
    }

    private static string[] CreateJapaneseCharacterMap()
    {
        string[] map = Enumerable.Repeat(" ", 256).ToArray();
        Set(map, 0x01, "\u3042");
        Set(map, 0x02, "\u3044");
        Set(map, 0x03, "\u3046");
        Set(map, 0x04, "\u3048");
        Set(map, 0x05, "\u304a");
        Set(map, 0x06, "\u304b");
        Set(map, 0x07, "\u304d");
        Set(map, 0x08, "\u304f");
        Set(map, 0x09, "\u3051");
        Set(map, 0x0A, "\u3053");
        Set(map, 0x0B, "\u3055");
        Set(map, 0x0C, "\u3057");
        Set(map, 0x0D, "\u3059");
        Set(map, 0x0E, "\u305b");
        Set(map, 0x0F, "\u305d");
        Set(map, 0x10, "\u305f");
        Set(map, 0x11, "\u3061");
        Set(map, 0x12, "\u3064");
        Set(map, 0x13, "\u3066");
        Set(map, 0x14, "\u3068");
        Set(map, 0x15, "\u306a");
        Set(map, 0x16, "\u306b");
        Set(map, 0x17, "\u306c");
        Set(map, 0x18, "\u306d");
        Set(map, 0x19, "\u306e");
        Set(map, 0x1A, "\u306f");
        Set(map, 0x1B, "\u3072");
        Set(map, 0x1C, "\u3075");
        Set(map, 0x1D, "\u3078");
        Set(map, 0x1E, "\u307b");
        Set(map, 0x1F, "\u307e");
        Set(map, 0x20, "\u307f");
        Set(map, 0x21, "\u3080");
        Set(map, 0x22, "\u3081");
        Set(map, 0x23, "\u3082");
        Set(map, 0x24, "\u3084");
        Set(map, 0x25, "\u3086");
        Set(map, 0x26, "\u3088");
        Set(map, 0x27, "\u3001");
        Set(map, 0x28, "\u3002");
        Set(map, 0x29, "\u3089");
        Set(map, 0x2A, "\u308a");
        Set(map, 0x2B, "\u308b");
        Set(map, 0x2C, "\u308c");
        Set(map, 0x2D, "\u308d");
        Set(map, 0x2E, "\u308f");
        Set(map, 0x2F, "\u3092");
        Set(map, 0x30, "\u3093");
        Set(map, 0x31, "\u301c");
        Set(map, 0x32, "\u2665");
        Set(map, 0x33, "\u304c");
        Set(map, 0x34, "\u304e");
        Set(map, 0x35, "\u3050");
        Set(map, 0x36, "\u3052");
        Set(map, 0x37, "\u3054");
        Set(map, 0x38, "\u3056");
        Set(map, 0x39, "\u3058");
        Set(map, 0x3A, "\u305a");
        Set(map, 0x3B, "\u305c");
        Set(map, 0x3C, "\u305e");
        Set(map, 0x3D, "\u3060");
        Set(map, 0x3E, "\u3062");
        Set(map, 0x3F, "\u3065");
        Set(map, 0x40, "\u3067");
        Set(map, 0x41, "\u3069");
        Set(map, 0x42, "\u3070");
        Set(map, 0x43, "\u3073");
        Set(map, 0x44, "\u3076");
        Set(map, 0x45, "\u3079");
        Set(map, 0x46, "\u307c");
        Set(map, 0x47, "\u3071");
        Set(map, 0x48, "\u3074");
        Set(map, 0x49, "\u3077");
        Set(map, 0x4A, "\u307a");
        Set(map, 0x4B, "\u307d");
        Set(map, 0x4C, "\u3063");
        Set(map, 0x4D, "\u3083");
        Set(map, 0x4E, "\u3085");
        Set(map, 0x4F, "\u3087");
        Set(map, 0x50, "\u30fb");
        Set(map, 0x51, "\u3041");
        Set(map, 0x52, "\u3043");
        Set(map, 0x53, "\u3045");
        Set(map, 0x54, "\u3047");
        Set(map, 0x55, "\u3049");
        Set(map, 0x56, "\u30a2");
        Set(map, 0x57, "\u30a4");
        Set(map, 0x58, "\u30a6");
        Set(map, 0x59, "\u30a8");
        Set(map, 0x5A, "\u30aa");
        Set(map, 0x5B, "\u30ab");
        Set(map, 0x5C, "\u30ad");
        Set(map, 0x5D, "\u30af");
        Set(map, 0x5E, "\u30b1");
        Set(map, 0x5F, "\u30b3");
        Set(map, 0x60, "\u30b5");
        Set(map, 0x61, "\u30b7");
        Set(map, 0x62, "\u30b9");
        Set(map, 0x63, "\u30bb");
        Set(map, 0x64, "\u30bd");
        Set(map, 0x65, "\u30bf");
        Set(map, 0x66, "\u30c1");
        Set(map, 0x67, "\u30c4");
        Set(map, 0x68, "\u30c6");
        Set(map, 0x69, "\u30c8");
        Set(map, 0x6A, "\u30ca");
        Set(map, 0x6B, "\u30cb");
        Set(map, 0x6C, "\u30cc");
        Set(map, 0x6D, "\u30cd");
        Set(map, 0x6E, "\u30ce");
        Set(map, 0x6F, "\u30cf");
        Set(map, 0x70, "\u30d2");
        Set(map, 0x71, "\u30d5");
        Set(map, 0x72, "\u30d8");
        Set(map, 0x73, "\u30db");
        Set(map, 0x74, "\u30de");
        Set(map, 0x75, "\u30df");
        Set(map, 0x76, "\u30e0");
        Set(map, 0x77, "\u30e1");
        Set(map, 0x78, "\u30e2");
        Set(map, 0x79, "\u30e4");
        Set(map, 0x7A, "\u30e6");
        Set(map, 0x7B, "\u30e8");
        Set(map, 0x7C, "!");
        Set(map, 0x7D, "?");
        Set(map, 0x7E, "\u30e9");
        Set(map, 0x7F, "\u30ea");
        Set(map, 0x80, "\u30eb");
        Set(map, 0x81, "\u30ec");
        Set(map, 0x82, "\u30ed");
        Set(map, 0x83, "\u30ef");
        Set(map, 0x84, "\u30f2");
        Set(map, 0x85, "\u30f3");
        Set(map, 0x86, "\u30f4");
        Set(map, 0x87, " ");
        Set(map, 0x88, "\u30ac");
        Set(map, 0x89, "\u30ae");
        Set(map, 0x8A, "\u30b0");
        Set(map, 0x8B, "\u30b2");
        Set(map, 0x8C, "\u30b4");
        Set(map, 0x8D, "\u30b6");
        Set(map, 0x8E, "\u30b8");
        Set(map, 0x8F, "\u30ba");
        Set(map, 0x90, "\u30bc");
        Set(map, 0x91, "\u30be");
        Set(map, 0x92, "\u30c0");
        Set(map, 0x93, "\u30c2");
        Set(map, 0x94, "\u30c5");
        Set(map, 0x95, "\u30c7");
        Set(map, 0x96, "\u30c9");
        Set(map, 0x97, "\u30d0");
        Set(map, 0x98, "\u30d3");
        Set(map, 0x99, "\u30d6");
        Set(map, 0x9A, "\u30d9");
        Set(map, 0x9B, "\u30dc");
        Set(map, 0x9C, "\u30d1");
        Set(map, 0x9D, "\u30d4");
        Set(map, 0x9E, "\u30d7");
        Set(map, 0x9F, "\u30da");
        Set(map, 0xA0, "\u30dd");
        Set(map, 0xA1, "\u30c3");
        Set(map, 0xA2, "\u30e3");
        Set(map, 0xA3, "\u30e5");
        Set(map, 0xA4, "\u30e7");
        Set(map, 0xA5, "\u30fc");
        Set(map, 0xA6, "\u30a1");
        Set(map, 0xA7, "\u30a3");
        Set(map, 0xA8, "\u30a5");
        Set(map, 0xA9, "\u30a7");
        Set(map, 0xAA, "\u30a9");
        Set(map, 0xAB, "A");
        Set(map, 0xAC, "B");
        Set(map, 0xAD, "C");
        Set(map, 0xAE, "D");
        Set(map, 0xAF, "E");
        Set(map, 0xB0, "F");
        Set(map, 0xB1, "G");
        Set(map, 0xB2, "H");
        Set(map, 0xB3, "I");
        Set(map, 0xB4, "J");
        Set(map, 0xB5, "K");
        Set(map, 0xB6, "L");
        Set(map, 0xB7, "M");
        Set(map, 0xB8, "N");
        Set(map, 0xB9, "O");
        Set(map, 0xBA, "P");
        Set(map, 0xBB, "Q");
        Set(map, 0xBC, "R");
        Set(map, 0xBD, "S");
        Set(map, 0xBE, "T");
        Set(map, 0xBF, "U");
        Set(map, 0xC0, "V");
        Set(map, 0xC1, "W");
        Set(map, 0xC2, "X");
        Set(map, 0xC3, "Y");
        Set(map, 0xC4, "Z");
        Set(map, 0xC5, "_");
        Set(map, 0xC6, "'");
        Set(map, 0xC7, ",");
        Set(map, 0xC8, ".");
        Set(map, 0xC9, "a");
        Set(map, 0xCA, "b");
        Set(map, 0xCB, "c");
        Set(map, 0xCC, "d");
        Set(map, 0xCD, "e");
        Set(map, 0xCE, "f");
        Set(map, 0xCF, "g");
        Set(map, 0xD0, "h");
        Set(map, 0xD1, "i");
        Set(map, 0xD2, "j");
        Set(map, 0xD3, "k");
        Set(map, 0xD4, "l");
        Set(map, 0xD5, "m");
        Set(map, 0xD6, "n");
        Set(map, 0xD7, "o");
        Set(map, 0xD8, "p");
        Set(map, 0xD9, "q");
        Set(map, 0xDA, "r");
        Set(map, 0xDB, "s");
        Set(map, 0xDC, "t");
        Set(map, 0xDD, "u");
        Set(map, 0xDE, "v");
        Set(map, 0xDF, "w");
        Set(map, 0xE0, "x");
        Set(map, 0xE1, "y");
        Set(map, 0xE2, "z");
        Set(map, 0xE3, "\ud83d\udcf1");
        Set(map, 0xE4, "\ud83d\ude05");
        Set(map, 0xE5, "\ud83d\ude04");
        Set(map, 0xE6, "_");
        Set(map, 0xE7, "0");
        Set(map, 0xE8, "1");
        Set(map, 0xE9, "2");
        Set(map, 0xEA, "3");
        Set(map, 0xEB, "4");
        Set(map, 0xEC, "5");
        Set(map, 0xED, "6");
        Set(map, 0xEE, "7");
        Set(map, 0xEF, "8");
        Set(map, 0xF0, "9");
        Set(map, 0xF1, "/");
        Set(map, 0xF2, ":");
        Set(map, 0xF3, "~");
        Set(map, 0xF4, "\"");
        Set(map, 0xF5, "@");
        return map;
    }

    private static void Set(string[] map, int index, string value) => map[index] = value;

    private readonly record struct ThumbnailOffsets(
        int Capture,
        int EdgeGains,
        int ExposureHigh,
        int ExposureLow,
        int EdgeModeVoltage,
        int VoltageOutputZero,
        int DitherSet,
        int Contrast);
}