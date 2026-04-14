using System.Text;
using GBTools.ImageSharp.GameBoyCamera.Codec;
using GBTools.ImageSharp.GameBoyCamera.Compatibility;
using GBTools.ImageSharp.GameBoyCamera.Tests.Fixtures;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace GBTools.ImageSharp.GameBoyCamera.Tests.Compatibility;

public class GameBoyCameraCompatibilityTests
{
    [Fact]
    public void LoadGbBin_decodes_raw_tile_payload()
    {
        byte[] tileBytes = CreateTileBytes(224);
        using MemoryStream stream = new();
        stream.Write("GB-BIN01"u8);
        stream.Write(tileBytes);
        stream.Position = 0;

        using Image<Rgba32> image = GameBoyCameraCompatibility.LoadGbBin(stream);

        Assert.Equal(128, image.Width);
        Assert.Equal(112, image.Height);
    }

    [Fact]
    public void LoadSaveDump_decodes_last_seen_photo()
    {
        byte[] save = GameBoyCameraFixtureFactory.CreateSaveFixture();

        using MemoryStream stream = new(save);
        using Image<Rgba32> image = GameBoyCameraCompatibility.LoadSaveDump(stream, new GameBoyCameraLoadOptions
        {
            FrameMode = GameBoyCameraFrameMode.Strip,
        });

        Assert.Equal(128, image.Width);
        Assert.Equal(112, image.Height);
        Assert.Equal(3, image.Frames.Count);
    }

    [Fact]
    public async Task ExportGbPrinterWebJsonAsync_round_trips_images()
    {
        using Image<Rgba32> original = CreateRenderedImage(GameBoyCameraConstants.RawPhotoTileWidth, GameBoyCameraConstants.RawPhotoTileHeight);
        using MemoryStream stream = new();

        await GameBoyCameraCompatibility.ExportGbPrinterWebJsonAsync(original, stream);
        stream.Position = 0;

        using Image<Rgba32> roundTripped = GameBoyCameraCompatibility.LoadGbPrinterWebJson(stream);

        Assert.Equal(original.Width, roundTripped.Width);
        Assert.Equal(original.Height, roundTripped.Height);
        Assert.Equal(original.Frames.Count, roundTripped.Frames.Count);
    }

    [Fact]
    public void LoadRomDump_reuses_save_dump_loader()
    {
        byte[] rom = GameBoyCameraFixtureFactory.CreateSaveFixture();

        using MemoryStream stream = new(rom);
        using Image<Rgba32> image = GameBoyCameraCompatibility.LoadRomDump(stream, new GameBoyCameraLoadOptions
        {
            FrameMode = GameBoyCameraFrameMode.Strip,
        });

        Assert.Equal(128, image.Width);
        Assert.Equal(112, image.Height);
    }

    [Fact]
    public void LoadSaveAlbum_sorts_by_album_index_when_requested()
    {
        byte[] save = GameBoyCameraFixtureFactory.CreateSaveFixture(reverseAlbumOrder: true);

        using MemoryStream stream = new(save);
        var album = GameBoyCameraCompatibility.LoadSaveAlbum(stream, new GameBoyCameraLoadOptions
        {
            FrameMode = GameBoyCameraFrameMode.Strip,
            ImportOrdering = GameBoyCameraImportOrdering.Album,
        });

        Assert.Equal(new[] { -1, 2, 5 }, album.Photos.Select(static photo => photo.Metadata!.AlbumIndex).ToArray());
    }

    [Fact]
    public void LoadSaveAlbum_can_decode_japanese_metadata()
    {
        byte[] save = GameBoyCameraFixtureFactory.CreateSaveFixture(cartIsJapanese: true);

        using MemoryStream stream = new(save);
        var album = GameBoyCameraCompatibility.LoadSaveAlbum(stream, new GameBoyCameraLoadOptions
        {
            FrameMode = GameBoyCameraFrameMode.Strip,
            CartIsJapanese = true,
        });

        Assert.Equal("PC-12345678", album.Photos[0].Metadata!.UserId);
        Assert.Contains("あいア", album.Photos[0].Metadata!.UserName);
    }

    [Fact]
    public void LoadSaveAlbum_extracts_pxlr_metadata()
    {
        byte[] save = GameBoyCameraFixtureFactory.CreateSaveFixture(romType: "pxlr");

        using MemoryStream stream = new(save);
        var album = GameBoyCameraCompatibility.LoadSaveAlbum(stream, new GameBoyCameraLoadOptions
        {
            FrameMode = GameBoyCameraFrameMode.Strip,
        });

        var metadata = album.Photos.First(static photo => photo.Metadata!.AlbumIndex >= 0).Metadata!;
        Assert.Equal("pxlr", metadata.RomType);
        Assert.Equal("32.0 (d)", metadata.Gain);
        Assert.Equal("Low/Off", metadata.DitherSet);
    }

    [Fact]
    public void LoadRomAlbum_extracts_photo_metadata_from_fixture()
    {
        byte[] rom = GameBoyCameraFixtureFactory.CreatePhotoRomFixture();

        using MemoryStream stream = new(rom);
        var album = GameBoyCameraCompatibility.LoadRomAlbum(stream, new GameBoyCameraLoadOptions
        {
            SourceKind = GameBoyCameraSourceKind.RomDump,
            FrameMode = GameBoyCameraFrameMode.Strip,
            ForceMagicCheck = false,
            IncludeLastSeen = false,
        });

        var metadata = Assert.Single(album.Photos).Metadata!;
        Assert.Equal("photo", metadata.RomType);
        Assert.Equal("positive", metadata.CaptureMode);
        Assert.Equal("0.992mV", metadata.VoltageOutput);
    }

    [Fact]
    public void LoadGbBinAlbum_pads_incomplete_last_tile_with_black_bytes()
    {
        byte[] gbBin = GameBoyCameraFixtureFactory.CreateGbBinFixture(incompleteTile: true);

        using MemoryStream stream = new(gbBin);
        var album = GameBoyCameraCompatibility.LoadGbBinAlbum(stream);

        var photo = Assert.Single(album.Photos);
        Assert.Equal(2, photo.TileGrid.Tiles.Count);
        Assert.Equal("42 BD 42 BD 42 BD 42 BD 42 BD 42 BD 42 BD 42 BD", GameBoyCameraTileTextCodec.FormatTile(photo.TileGrid.Tiles[0]));
        Assert.Equal("43 FF FF FF FF FF FF FF FF FF FF FF FF FF FF FF", GameBoyCameraTileTextCodec.FormatTile(photo.TileGrid.Tiles[1]));
    }

    [Fact]
    public void LoadGbPrinterWebAlbum_supports_legacy_frame_payloads()
    {
        byte[] json = GameBoyCameraFixtureFactory.CreateJsonFixture(legacyFramePayload: true);

        using MemoryStream stream = new(json);
        var album = GameBoyCameraCompatibility.LoadGbPrinterWebAlbum(stream, new GameBoyCameraLoadOptions
        {
            FrameMode = GameBoyCameraFrameMode.Extract,
        });

        var photo = Assert.Single(album.Photos);
        Assert.NotNull(photo.FrameOverlay);
        Assert.Equal(GameBoyCameraConstants.RawPhotoTileWidth, photo.TileGrid.WidthInTiles);
        Assert.Equal(GameBoyCameraConstants.RawPhotoTileHeight, photo.TileGrid.HeightInTiles);
    }

    [Fact]
    public void ExportGbBin_writes_valid_header_and_tile_payload()
    {
        using Image<Rgba32> image = CreateRenderedImage(GameBoyCameraConstants.RawPhotoTileWidth, GameBoyCameraConstants.RawPhotoTileHeight);
        using MemoryStream stream = new();

        GameBoyCameraCompatibility.ExportGbBin(image, stream);

        byte[] bytes = stream.ToArray();
        Assert.True(bytes.AsSpan(0, 8).SequenceEqual("GB-BIN01"u8));
        Assert.Equal(8 + (224 * 16), bytes.Length);
    }

    private static byte[] CreateTileBytes(int tileCount)
    {
        byte[] bytes = new byte[tileCount * GameBoyCameraConstants.TileByteCount];
        for (int tileIndex = 0; tileIndex < tileCount; tileIndex++)
        {
            byte value = (byte)(tileIndex % 4 == 0 ? 0xFF : 0x00);
            for (int byteIndex = 0; byteIndex < GameBoyCameraConstants.TileByteCount; byteIndex++)
            {
                bytes[(tileIndex * GameBoyCameraConstants.TileByteCount) + byteIndex] = value;
            }
        }

        bytes[0] = 0xFF;
        bytes[1] = 0xAA;
        return bytes;
    }

    private static Image<Rgba32> CreateRenderedImage(int widthInTiles, int heightInTiles)
    {
        string[] rawTiles = Enumerable.Range(0, widthInTiles * heightInTiles)
            .Select(index => string.Join(" ", Enumerable.Repeat((index % 256).ToString("X2"), 16)))
            .ToArray();
        return GameBoyCameraTileGridRenderer.Render(GameBoyCameraTileGridFactory.CreateFromTextTiles(rawTiles, widthInTiles));
    }
}