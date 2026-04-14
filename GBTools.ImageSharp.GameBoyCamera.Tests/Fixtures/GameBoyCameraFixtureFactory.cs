using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GBTools.ImageSharp.GameBoyCamera.Codec;
using GBTools.ImageSharp.GameBoyCamera.Model;

namespace GBTools.ImageSharp.GameBoyCamera.Tests.Fixtures;

internal static class GameBoyCameraFixtureFactory
{
    public static byte[] CreateSaveFixture(
        bool cartIsJapanese = false,
        string romType = "stock",
        bool includeDeleted = false,
        bool includeLastSeen = true,
        bool reverseAlbumOrder = false)
    {
        byte[] data = new byte[0x5000];
        foreach (int offset in new[] { 0x10D2, 0x11AB, 0x11D0, 0x11F5 })
        {
            Encoding.ASCII.GetBytes("Magic").CopyTo(data, offset);
        }

        if (includeLastSeen)
        {
            FillPhotoSlot(data, 0x0000, 0x31, cartIsJapanese, romType, albumIndex: -1, frameNumber: 0);
        }

        FillPhotoSlot(data, 0x2000, 0x52, cartIsJapanese, romType, albumIndex: reverseAlbumOrder ? 5 : 2, frameNumber: 1);
        FillPhotoSlot(data, 0x3000, 0x74, cartIsJapanese, romType, albumIndex: reverseAlbumOrder ? 2 : 5, frameNumber: 2, isDeleted: includeDeleted);

        return data;
    }

    public static byte[] CreatePhotoRomFixture()
    {
        byte[] data = CreateSaveFixture(romType: "photo", includeLastSeen: false);
        Array.Resize(ref data, 0x100000);
        Encoding.ASCII.GetBytes("PHOTO").CopyTo(data, 0x134);
        FillPhotoSlot(data, 0x22000, 0x91, cartIsJapanese: false, romType: "photo", albumIndex: 1, frameNumber: 3);
        return data;
    }

    public static byte[] CreateGbBinFixture(bool incompleteTile = false)
    {
        byte[] tileBytes = CreateTileBytes(224, 0x42);
        if (incompleteTile)
        {
            tileBytes = tileBytes[..17];
        }

        using MemoryStream stream = new();
        stream.Write("GB-BIN01"u8);
        stream.Write(tileBytes);
        return stream.ToArray();
    }

    public static byte[] CreateJsonFixture(bool legacyFramePayload = false)
    {
        string[] rawTiles = Enumerable.Range(0, 224)
            .Select(index => string.Join(" ", Enumerable.Repeat(((index + 16) % 256).ToString("X2"), 16)))
            .ToArray();
        string imagePayload = DeflateLatin1(string.Join("\n", rawTiles.Select(static tile => tile.Replace(" ", string.Empty, StringComparison.Ordinal))));
        string imageHash = ComputeHash(imagePayload);

        string[] frameTiles = Enumerable.Range(0, 136)
            .Select(index => string.Join(" ", Enumerable.Repeat((index % 256).ToString("X2"), 16)))
            .ToArray();

        string framePayload = legacyFramePayload
            ? DeflateLatin1(string.Join("\n", frameTiles.Select(static tile => tile.Replace(" ", string.Empty, StringComparison.Ordinal))))
            : DeflateLatin1(GameBoyCameraFrameCodec.SerializeFrameOverlay(GameBoyCameraFrameCodec.ParseFrameOverlay(string.Join("\n", frameTiles))));
        string frameHash = ComputeHash(framePayload);

        var document = new Dictionary<string, object?>
        {
            ["state"] = new Dictionary<string, object?>
            {
                ["lastUpdateUTC"] = 1,
                ["version"] = 1,
                ["images"] = new[]
                {
                    new Dictionary<string, object?>
                    {
                        ["hash"] = imageHash,
                        ["created"] = "2026-04-14T00:00:00.0000000Z",
                        ["title"] = "Fixture",
                        ["frame"] = frameHash,
                        ["tags"] = Array.Empty<string>(),
                        ["palette"] = "default",
                        ["invertPalette"] = false,
                        ["framePalette"] = "default",
                        ["invertFramePalette"] = false,
                        ["lines"] = 14,
                    },
                },
                ["frames"] = new[]
                {
                    new Dictionary<string, object?>
                    {
                        ["id"] = "fixture01",
                        ["hash"] = frameHash,
                        ["name"] = "Fixture Frame",
                        ["lines"] = 18,
                    },
                },
            },
            [imageHash] = imagePayload,
            [$"frame-{frameHash}"] = framePayload,
        };

        return JsonSerializer.SerializeToUtf8Bytes(document, new JsonSerializerOptions { WriteIndented = true });
    }

    private static void FillPhotoSlot(byte[] data, int baseAddress, byte seed, bool cartIsJapanese, string romType, int albumIndex, int frameNumber, bool isDeleted = false)
    {
        CreateTileBytes(224, seed).CopyTo(data, baseAddress);
        byte[] thumbnail = new byte[GbcThumbnail.ByteLength];
        switch (romType)
        {
            case "pxlr":
                foreach (int index in Enumerable.Range(0xC8, 8).Concat(Enumerable.Range(0xD8, 8)).Concat(Enumerable.Range(0xE8, 8)).Concat(Enumerable.Range(0xF8, 8)))
                {
                    thumbnail[index] = 0xFF;
                }
                thumbnail[0x20] = 0x03;
                thumbnail[0x30] = 0x02;
                thumbnail[0x10] = 0b00011000;
                thumbnail[0xC6] = 0b00101010;
                thumbnail[0xD6] = 0b01111110;
                thumbnail[0xE6] = 0b00000011;
                thumbnail[0xF6] = 0x55;
                break;
            case "photo":
                thumbnail[0xCB] = 0x02;
                thumbnail[0xCA] = 0x01;
                thumbnail[0xC8] = 0b00000010;
                thumbnail[0xC9] = 0b11011111;
                thumbnail[0xCC] = 0b01111101;
                thumbnail[0xCD] = 0b10111111;
                break;
        }

        thumbnail.CopyTo(data, baseAddress + 0x0E00);

        data[baseAddress + 0x0F00] = 0x23;
        data[baseAddress + 0x0F01] = 0x45;
        data[baseAddress + 0x0F02] = 0x67;
        data[baseAddress + 0x0F03] = 0x89;

        if (cartIsJapanese)
        {
            data[baseAddress + 0x0F04] = 0x01;
            data[baseAddress + 0x0F05] = 0x02;
            data[baseAddress + 0x0F06] = 0x56;
            data[baseAddress + 0x0F07] = 0x87;
        }
        else
        {
            data[baseAddress + 0x0F04] = 0x56;
            data[baseAddress + 0x0F05] = 0x57;
            data[baseAddress + 0x0F06] = 0x58;
            data[baseAddress + 0x0F07] = 0x87;
        }

        data[baseAddress + 0x0F0D] = 0x05;
        data[baseAddress + 0x0F0E] = 0x2A;
        data[baseAddress + 0x0F0F] = 0xAA;
        data[baseAddress + 0x0F10] = 0x23;
        data[baseAddress + 0x0F11] = 0x42;
        data[baseAddress + 0x0F15] = 0x56;
        data[baseAddress + 0x0F16] = 0x57;
        data[baseAddress + 0x0F17] = 0x83;
        data[baseAddress + 0x0F18] = 0xBA;
        data[baseAddress + 0x0F19] = 0xBB;
        data[baseAddress + 0x0F33] = 0x01;
        data[baseAddress + 0x0F54] = (byte)frameNumber;

        int inBankAddress = baseAddress % 0x20000;
        int cartIndex = (inBankAddress / 0x1000) - 2;
        if (cartIndex >= 0)
        {
            data[0x11B2 + cartIndex] = isDeleted ? (byte)255 : (byte)albumIndex;
        }
    }

    private static byte[] CreateTileBytes(int tileCount, byte seed)
    {
        byte[] bytes = new byte[tileCount * GameBoyCameraConstants.TileByteCount];
        for (int tileIndex = 0; tileIndex < tileCount; tileIndex++)
        {
            byte low = (byte)(seed + tileIndex);
            byte high = (byte)(~low);
            for (int row = 0; row < 8; row++)
            {
                bytes[(tileIndex * 16) + (row * 2)] = low;
                bytes[(tileIndex * 16) + (row * 2) + 1] = high;
            }
        }

        return bytes;
    }

    private static string DeflateLatin1(string payload)
    {
        using MemoryStream output = new();
        using (ZLibStream zlib = new(output, CompressionLevel.Optimal, leaveOpen: true))
        using (StreamWriter writer = new(zlib, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(payload);
        }

        return Encoding.Latin1.GetString(output.ToArray());
    }

    private static string ComputeHash(string payload)
    {
        byte[] bytes = Encoding.Latin1.GetBytes(payload);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}