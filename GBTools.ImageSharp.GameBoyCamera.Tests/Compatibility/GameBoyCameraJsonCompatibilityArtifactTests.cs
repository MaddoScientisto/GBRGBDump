using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Globalization;
using GBTools.ImageSharp.GameBoyCamera.Codec;
using GBTools.ImageSharp.GameBoyCamera.Compatibility;
using GBTools.ImageSharp.GameBoyCamera.Model;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace GBTools.ImageSharp.GameBoyCamera.Tests.Compatibility;

public class GameBoyCameraJsonCompatibilityArtifactTests
{
    [Fact]
    public void LoadSaveAlbum_real_camera_dump_imports_multiple_photos()
    {
        using FileStream stream = File.OpenRead(GetArtifactPath("20240806_181557_GB_C54AC95A_Game Boy Camera.sav"));

        GbcAlbum album = GameBoyCameraCompatibility.LoadSaveAlbum(stream, new GameBoyCameraLoadOptions
        {
            FrameMode = GameBoyCameraFrameMode.Keep,
        });

        Assert.True(album.Photos.Count >= 2);
        Assert.Equal(20, album.Photos[0].TileGrid.WidthInTiles);
        Assert.Equal(18, album.Photos[0].TileGrid.HeightInTiles);
        Assert.Equal("photo", album.Photos[0].Metadata?.RomType);
        Assert.Equal("15ms", album.Photos[0].Metadata?.Exposure);
        Assert.Equal("14ms", album.Photos[1].Metadata?.Exposure);
    }

    [Fact]
    public async Task ExportGbPrinterWebJsonAsync_matches_single_photo_artifact_shape()
    {
        await AssertExportMatchesArtifactAsync("gbweb_exported.json", photoCount: 1);
    }

    [Fact]
    public async Task ExportGbPrinterWebJsonAsync_matches_two_photo_artifact_shape()
    {
        await AssertExportMatchesArtifactAsync("gbweb_exported_2pics.json", photoCount: 2);
    }

    [Fact]
    public void LoadGbPrinterWebAlbum_backup_archive_imports_photos()
    {
        int loadedPhotoCount = LoadAlbumWithIsolatedFailures("backup_images.json", out IReadOnlyList<string> failures);

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        Assert.Equal(13_419, loadedPhotoCount);
    }

    [Fact]
    public void LoadGbPrinterWebAlbum_backup_sample_imports_expected_hashes()
    {
        using FileStream stream = File.OpenRead(GetArtifactPath("backup_images_sample.json"));

        GbcAlbum album = GameBoyCameraCompatibility.LoadGbPrinterWebAlbum(stream, new GameBoyCameraLoadOptions
        {
            FrameMode = GameBoyCameraFrameMode.Keep,
        });

        Assert.Equal(4, album.Photos.Count);
        Assert.All(album.Photos, static photo => Assert.True(photo.TileGrid.HeightInTiles > 0));
    }

    [Fact]
    public void LoadGbPrinterWebAlbum_backup_empty_payload_imports_blank_single_tile()
    {
        using FileStream stream = File.OpenRead(GetArtifactPath("backup_images_empty_payload.json"));

        GbcAlbum album = GameBoyCameraCompatibility.LoadGbPrinterWebAlbum(stream, new GameBoyCameraLoadOptions
        {
            FrameMode = GameBoyCameraFrameMode.Keep,
        });

        GbcPhoto photo = Assert.Single(album.Photos);
        Assert.Equal(1, photo.TileGrid.WidthInTiles);
        Assert.Equal(1, photo.TileGrid.HeightInTiles);
        Assert.Equal(GameBoyCameraConstants.TileByteCount, photo.TileGrid.Tiles[0].Bytes.Length);
        Assert.All(photo.TileGrid.Tiles[0].Bytes.Span.ToArray(), static value => Assert.Equal((byte)0, value));
    }

    [Fact]
    public void LoadGbPrinterWebAlbum_rgb_export_imports_rendered_color_photo()
    {
        using FileStream stream = File.OpenRead(GetArtifactPath("rgb_export.json"));

        GbcAlbum album = GameBoyCameraCompatibility.LoadGbPrinterWebAlbum(stream, new GameBoyCameraLoadOptions
        {
            FrameMode = GameBoyCameraFrameMode.Keep,
        });

        GbcPhoto photo = Assert.Single(album.Photos);
        Assert.True(photo.HasRenderedImage);
        Assert.True(photo.RenderedWidth is 128 or 160);
        Assert.True(photo.RenderedHeight is 112 or 144);

        using FileStream renderStream = File.OpenRead(GetArtifactPath("rgb_export.json"));
        using Image<Rgba32> image = GameBoyCameraCompatibility.LoadGbPrinterWebJson(renderStream);

        Assert.Equal(photo.RenderedWidth, image.Width);
        Assert.Equal(photo.RenderedHeight, image.Height);
        Assert.Contains(EnumeratePixels(image), static pixel => pixel.R != pixel.G || pixel.G != pixel.B);
    }

    [Fact]
    public void LoadGbPrinterWebAlbum_rgb_export_good_renders_green_dominant_photo()
    {
        using FileStream stream = File.OpenRead(GetArtifactPath("rgb_export_good.json"));
        using Image<Rgba32> image = GameBoyCameraCompatibility.LoadGbPrinterWebJson(stream);

        (double averageRed, double averageGreen, double averageBlue) = GetAverageChannels(image);

        Assert.True(averageGreen > averageRed, $"Expected green average to exceed red, got G={averageGreen:F2}, R={averageRed:F2}.");
        Assert.True(averageGreen > averageBlue, $"Expected green average to exceed blue, got G={averageGreen:F2}, B={averageBlue:F2}.");
        Assert.True(averageGreen - averageRed >= 10d, $"Expected a visibly green image, got delta {averageGreen - averageRed:F2}.");
    }

    [Fact]
    public void LoadGbPrinterWebAlbum_pics_with_frames_preserves_multi_image_order_and_mixed_metadata()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(GetArtifactPath("pics_with_frames.json")));
        JsonElement[] expectedImages = document.RootElement.GetProperty("state").GetProperty("images").EnumerateArray().ToArray();

        using FileStream stream = File.OpenRead(GetArtifactPath("pics_with_frames.json"));
        GbcAlbum album = GameBoyCameraCompatibility.LoadGbPrinterWebAlbum(stream, new GameBoyCameraLoadOptions
        {
            FrameMode = GameBoyCameraFrameMode.Keep,
        });

        Assert.Equal(expectedImages.Length, album.Photos.Count);
        Assert.Null(album.Photos[0].Metadata);
        Assert.Equal("806ms", album.Photos[1].Metadata?.Exposure);
        Assert.Equal("3.3ms", album.Photos[2].Metadata?.Exposure);
    }

    [Fact]
    public void LoadSaveAlbum_picnrec_export_imports_single_photo()
    {
        using FileStream stream = File.OpenRead(GetArtifactPath("picnrec_export_test.sav"));

        GbcAlbum album = GameBoyCameraCompatibility.LoadSaveAlbum(stream, new GameBoyCameraLoadOptions
        {
            FrameMode = GameBoyCameraFrameMode.Keep,
        });

        GbcPhoto photo = Assert.Single(album.Photos);
        Assert.Equal(16, photo.TileGrid.WidthInTiles);
        Assert.Equal(14, photo.TileGrid.HeightInTiles);

        using Image<Rgba32> rendered = GameBoyCameraImageCodec.RenderPhoto(photo);
        using Image<Rgba32> expected = Image.Load<Rgba32>(GetArtifactPath("picnrec_export_test_expected.png"));

        Assert.Equal(expected.Width, rendered.Width);
        Assert.Equal(expected.Height, rendered.Height);

        for (int y = 0; y < expected.Height; y++)
        {
            for (int x = 0; x < expected.Width; x++)
            {
                Assert.Equal(expected[x, y], rendered[x, y]);
            }
        }
    }

    [Fact]
    public void LoadGbPrinterWebAlbum_android_gallery_export_imports_photos()
    {
        using FileStream stream = File.OpenRead(GetArtifactPath("android_gallery_export.json"));

        GbcAlbum album = GameBoyCameraCompatibility.LoadGbPrinterWebAlbum(stream, new GameBoyCameraLoadOptions
        {
            FrameMode = GameBoyCameraFrameMode.Keep,
        });

        Assert.Equal(3, album.Photos.Count);
        Assert.All(album.Photos, static photo =>
        {
            Assert.Equal(20, photo.TileGrid.WidthInTiles);
            Assert.Equal(18, photo.TileGrid.HeightInTiles);
        });
        Assert.Equal("79ms", album.Photos[0].Metadata?.Exposure);
        Assert.Equal("Photo!", album.Photos[0].Metadata?.RomType);
    }

    [Fact]
    public async Task ExportGbPrinterWebJsonAsync_preserves_rgbn_payloads_for_imported_rgb_photo()
    {
        using JsonDocument expectedDocument = JsonDocument.Parse(File.ReadAllText(GetArtifactPath("rgb_export.json")));
        JsonElement expectedRoot = expectedDocument.RootElement;
        JsonElement expectedImage = Assert.Single(expectedRoot.GetProperty("state").GetProperty("images").EnumerateArray());

        using FileStream loadStream = File.OpenRead(GetArtifactPath("rgb_export.json"));
        GbcAlbum album = GameBoyCameraCompatibility.LoadGbPrinterWebAlbum(loadStream, new GameBoyCameraLoadOptions
        {
            FrameMode = GameBoyCameraFrameMode.Keep,
        });

        using MemoryStream exportStream = new();
        await GameBoyCameraCompatibility.ExportGbPrinterWebJsonAsync(album, exportStream, new GameBoyCameraJsonExportOptions
        {
            LastUpdateUtc = DateTimeOffset.FromUnixTimeSeconds(expectedRoot.GetProperty("state").GetProperty("lastUpdateUTC").GetInt64()),
            TitleFactory = (_, _) => expectedImage.GetProperty("title").GetString() ?? string.Empty,
            CreatedFactory = (_, _) => expectedImage.GetProperty("created").GetString() ?? string.Empty,
        });
        exportStream.Position = 0;

        using JsonDocument actualDocument = JsonDocument.Parse(exportStream);
        JsonElement actualRoot = actualDocument.RootElement;
        JsonElement actualImage = Assert.Single(actualRoot.GetProperty("state").GetProperty("images").EnumerateArray());

        Assert.Equal(expectedImage.GetProperty("hash").GetString(), actualImage.GetProperty("hash").GetString());
        Assert.True(JsonElement.DeepEquals(expectedImage.GetProperty("palette"), actualImage.GetProperty("palette")));
        Assert.True(JsonElement.DeepEquals(expectedImage.GetProperty("hashes"), actualImage.GetProperty("hashes")));

        foreach (string channelName in new[] { "r", "g", "b" })
        {
            string channelHash = expectedImage.GetProperty("hashes").GetProperty(channelName).GetString()!;
            Assert.Equal(expectedRoot.GetProperty(channelHash).GetString(), actualRoot.GetProperty(channelHash).GetString());
        }
    }

    private static async Task AssertExportMatchesArtifactAsync(string artifactFileName, int photoCount)
    {
        using FileStream saveStream = File.OpenRead(GetArtifactPath("20240806_181557_GB_C54AC95A_Game Boy Camera.sav"));
        GbcAlbum loadedAlbum = GameBoyCameraCompatibility.LoadSaveAlbum(saveStream, new GameBoyCameraLoadOptions
        {
            FrameMode = GameBoyCameraFrameMode.Keep,
        });

        using JsonDocument expectedDocument = JsonDocument.Parse(File.ReadAllText(GetArtifactPath(artifactFileName)));
        JsonElement expectedState = expectedDocument.RootElement.GetProperty("state");
        JsonElement expectedImages = expectedState.GetProperty("images");

        GbcAlbum exportAlbum = new(loadedAlbum.Metadata, loadedAlbum.Photos.Take(photoCount).ToArray());
        GameBoyCameraJsonExportOptions exportOptions = new()
        {
            LastUpdateUtc = DateTimeOffset.FromUnixTimeSeconds(expectedState.GetProperty("lastUpdateUTC").GetInt64()),
            TitleFactory = (index, _) => expectedImages[index].GetProperty("title").GetString()!,
            CreatedFactory = (index, _) => expectedImages[index].GetProperty("created").GetString()!,
        };

        using MemoryStream stream = new();
        await GameBoyCameraCompatibility.ExportGbPrinterWebJsonAsync(exportAlbum, stream, exportOptions);
        stream.Position = 0;

        using JsonDocument actualDocument = JsonDocument.Parse(stream);
        JsonElement actualRoot = actualDocument.RootElement;
        JsonElement actualState = actualRoot.GetProperty("state");
        JsonElement actualImages = actualState.GetProperty("images");

        Assert.Equal(expectedState.GetProperty("version").GetInt32(), actualState.GetProperty("version").GetInt32());
        Assert.Equal(expectedState.GetProperty("lastUpdateUTC").GetInt64(), actualState.GetProperty("lastUpdateUTC").GetInt64());
        Assert.False(actualState.TryGetProperty("frames", out _));
        Assert.Equal(photoCount, actualImages.GetArrayLength());

        for (int index = 0; index < photoCount; index++)
        {
            JsonElement expectedImage = expectedImages[index];
            JsonElement actualImage = actualImages[index];

            Assert.Matches("^[0-9a-f]{40}$", actualImage.GetProperty("hash").GetString()!);
            Assert.Equal(expectedImage.GetProperty("title").GetString(), actualImage.GetProperty("title").GetString());
            Assert.Equal(expectedImage.GetProperty("created").GetString(), actualImage.GetProperty("created").GetString());
            Assert.Equal(expectedImage.GetProperty("lines").GetInt32(), actualImage.GetProperty("lines").GetInt32());
            Assert.Equal(expectedImage.GetProperty("palette").GetString(), actualImage.GetProperty("palette").GetString());
            Assert.Equal(expectedImage.GetProperty("framePalette").GetString(), actualImage.GetProperty("framePalette").GetString());
            Assert.Equal(expectedImage.GetProperty("invertFramePalette").GetBoolean(), actualImage.GetProperty("invertFramePalette").GetBoolean());
            Assert.Equal(expectedImage.GetProperty("invertPalette").GetBoolean(), actualImage.GetProperty("invertPalette").GetBoolean());
            Assert.Equal(expectedImage.GetProperty("frame").GetString(), actualImage.GetProperty("frame").GetString());
            Assert.Equal(expectedImage.GetProperty("tags").GetArrayLength(), actualImage.GetProperty("tags").GetArrayLength());

            JsonElement expectedMeta = expectedImage.GetProperty("meta");
            JsonElement actualMeta = actualImage.GetProperty("meta");
            Assert.True(JsonElement.DeepEquals(expectedMeta, actualMeta), $"Metadata mismatch for image index {index}.");

            string expectedPayload = InflatePayload(expectedDocument.RootElement.GetProperty(expectedImage.GetProperty("hash").GetString()!).GetString()!);
            string actualPayload = InflatePayload(actualRoot.GetProperty(actualImage.GetProperty("hash").GetString()!).GetString()!);
            Assert.Equal(expectedPayload, actualPayload);
        }
    }

    private static string InflatePayload(string payload)
    {
        byte[] data = Encoding.Latin1.GetBytes(payload);
        using MemoryStream input = new(data);
        using ZLibStream zlib = new(input, CompressionMode.Decompress);
        using StreamReader reader = new(zlib, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static (double AverageRed, double AverageGreen, double AverageBlue) GetAverageChannels(Image<Rgba32> image)
    {
        double totalRed = 0;
        double totalGreen = 0;
        double totalBlue = 0;
        long count = 0;

        foreach (Rgba32 pixel in EnumeratePixels(image))
        {
            totalRed += pixel.R;
            totalGreen += pixel.G;
            totalBlue += pixel.B;
            count++;
        }

        return (totalRed / count, totalGreen / count, totalBlue / count);
    }

    private static string GetArtifactPath(string fileName)
        => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "GBTools.ImageSharp.GameBoyCamera.Tests", "TestArtifacts", fileName));

    private static int LoadAlbumWithIsolatedFailures(string artifactFileName, out IReadOnlyList<string> failures)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(GetArtifactPath(artifactFileName)));
        JsonElement root = document.RootElement;
        JsonElement[] images = root.GetProperty("state").GetProperty("images").EnumerateArray().ToArray();

        List<string> failingEntries = [];
        int loadedPhotoCount = 0;
        foreach ((JsonElement image, int index) in images.Select((image, index) => (image, index)))
        {
            string hash = image.GetProperty("hash").GetString()!;
            try
            {
                using MemoryStream stream = CreateSingleImageArchive(root, image);
                GbcAlbum album = GameBoyCameraCompatibility.LoadGbPrinterWebAlbum(stream, new GameBoyCameraLoadOptions
                {
                    FrameMode = GameBoyCameraFrameMode.Keep,
                });
                Assert.Single(album.Photos);
                Assert.True(album.Photos[0].TileGrid.WidthInTiles > 0);
                Assert.True(album.Photos[0].TileGrid.HeightInTiles > 0);
                loadedPhotoCount += album.Photos.Count;
            }
            catch (Exception ex)
            {
                failingEntries.Add(FormattableString.Invariant($"[{index.ToString(CultureInfo.InvariantCulture)}] {hash} {image.GetProperty("title").GetString()} :: {ex.GetType().Name}: {ex.Message}"));
            }
        }

        failures = failingEntries;
        return loadedPhotoCount;
    }

    private static MemoryStream CreateSingleImageArchive(JsonElement root, JsonElement image)
    {
        string hash = image.GetProperty("hash").GetString()!;
        object payload = new Dictionary<string, object?>
        {
            ["state"] = new Dictionary<string, object?>
            {
                ["images"] = new[]
                {
                    JsonSerializer.Deserialize<object>(image.GetRawText()),
                },
                ["lastUpdateUTC"] = root.GetProperty("state").GetProperty("lastUpdateUTC").GetInt64(),
                ["version"] = root.GetProperty("state").GetProperty("version").GetInt32(),
            },
            [hash] = root.GetProperty(hash).GetString(),
        };

        MemoryStream stream = new();
        JsonSerializer.Serialize(stream, payload);
        stream.Position = 0;
        return stream;
    }

    private static IEnumerable<Rgba32> EnumeratePixels(Image<Rgba32> image)
    {
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                yield return image[x, y];
            }
        }
    }
}