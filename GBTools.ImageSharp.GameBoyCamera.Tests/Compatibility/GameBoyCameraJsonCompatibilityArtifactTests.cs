using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Globalization;
using GBTools.ImageSharp.GameBoyCamera.Compatibility;
using GBTools.ImageSharp.GameBoyCamera.Model;

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
}