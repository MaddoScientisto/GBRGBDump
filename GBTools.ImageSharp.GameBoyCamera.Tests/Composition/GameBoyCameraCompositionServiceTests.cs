using System.Text.Json;
using GBTools.ImageSharp.GameBoyCamera.Codec;
using GBTools.ImageSharp.GameBoyCamera.Composition;
using GBTools.ImageSharp.GameBoyCamera.Compatibility;
using GBTools.ImageSharp.GameBoyCamera.Metadata;
using GBTools.ImageSharp.GameBoyCamera.Model;
using GBTools.ImageSharp.GameBoyCamera.Tests.Fixtures;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace GBTools.ImageSharp.GameBoyCamera.Tests.Composition;

public class GameBoyCameraCompositionServiceTests
{
    [Fact]
    public void CreateRgbPhotos_sequential_merges_monochrome_sources_into_color_outputs()
    {
        IReadOnlyList<GbcPhoto> sources = Enumerable.Range(0, 5).Select(_ => CreateSolidPhoto(255))
            .Concat(Enumerable.Range(0, 5).Select(_ => CreateSolidPhoto(170)))
            .Concat(Enumerable.Range(0, 5).Select(_ => CreateSolidPhoto(85)))
            .ToArray();

        IReadOnlyList<GbcPhoto> results = GameBoyCameraCompositionService.CreateRgbPhotos(sources);

        Assert.Equal(5, results.Count);
        Assert.All(results, static photo => Assert.NotNull(photo.RgbnData));

        using Image<Rgba32> rendered = GameBoyCameraImageCodec.RenderPhoto(results[0]);
        Rgba32 pixel = rendered[0, 0];

        Assert.Equal((byte)255, pixel.R);
        Assert.Equal((byte)170, pixel.G);
        Assert.Equal((byte)85, pixel.B);
    }

    [Fact]
    public void CreateAveragePhotos_blends_generated_rgb_sources_using_alpha_stack_algorithm()
    {
        IReadOnlyList<GbcPhoto> sources =
        [
            CreateSolidPhoto(255),
            CreateSolidPhoto(0),
            CreateSolidPhoto(0),
            CreateSolidPhoto(0),
            CreateSolidPhoto(0),
            CreateSolidPhoto(255),
        ];

        GameBoyCameraAverageCompositionResult result = GameBoyCameraCompositionService.CreateAveragePhotos(sources);

        GbcPhoto average = Assert.Single(result.AveragePhotos);
        Assert.NotNull(average.AverageData);
        Assert.Equal(new[] { 6 }, result.SourceGroupSizes);

        using Image<Rgba32> rendered = GameBoyCameraImageCodec.RenderPhoto(average);
        Rgba32 pixel = rendered[0, 0];

        Assert.Equal((byte)128, pixel.R);
        Assert.Equal((byte)0, pixel.G);
        Assert.Equal((byte)128, pixel.B);
    }

    [Fact]
    public void CreateAveragePhotos_full_bank_creates_group_and_fullbank_averages()
    {
        IReadOnlyList<GbcPhoto> sources = Enumerable.Range(0, 10).Select(_ => CreateSolidPhoto(255))
            .Concat(Enumerable.Range(0, 10).Select(_ => CreateSolidPhoto(170)))
            .Concat(Enumerable.Range(0, 10).Select(_ => CreateSolidPhoto(85)))
            .ToArray();

        GameBoyCameraAverageCompositionResult result = GameBoyCameraCompositionService.CreateAveragePhotos(
            sources,
            new GameBoyCameraAverageCompositionOptions(
                GameBoyCameraCompositionChannelOrder.Sequential,
                GameBoyCameraAverageCompositionMode.FullBank));

        Assert.Equal(10, result.RgbPhotos.Count);
        Assert.Equal(3, result.AveragePhotos.Count);
        Assert.Equal(new[] { 15, 15 }, result.SourceGroupSizes);
        Assert.All(result.AveragePhotos, static photo => Assert.NotNull(photo.AverageData));
    }

    [Fact]
    public async Task ExportGbPrinterWebJsonAsync_round_trips_average_composite_images()
    {
        IReadOnlyList<GbcPhoto> sources = Enumerable.Range(0, 2).Select(_ => CreateSolidPhoto(255))
            .Concat(Enumerable.Range(0, 2).Select(_ => CreateSolidPhoto(170)))
            .Concat(Enumerable.Range(0, 2).Select(_ => CreateSolidPhoto(85)))
            .ToArray();

        GbcPhoto average = Assert.Single(GameBoyCameraCompositionService.CreateAveragePhotos(sources).AveragePhotos);

        GbcAlbum album = new(new GameBoyCameraAlbumMetadata(GameBoyCameraSourceKind.GbPrinterWebJson, null, 1, null, 1), [average]);

        await using MemoryStream stream = new();
        await GameBoyCameraCompatibility.ExportGbPrinterWebJsonAsync(album, stream, new GameBoyCameraJsonExportOptions
        {
            LastUpdateUtc = DateTimeOffset.UnixEpoch,
            TitleFactory = static (_, _) => "Average Composite",
            CreatedFactory = static (_, _) => "2026-04-14 23:00:00:000",
        });

        stream.Position = 0;
        using JsonDocument document = JsonDocument.Parse(stream);
        JsonElement image = Assert.Single(document.RootElement.GetProperty("state").GetProperty("images").EnumerateArray());
        Assert.Equal("average", image.GetProperty("type").GetString());
        Assert.Equal("average", image.GetProperty("composite").GetProperty("kind").GetString());
        Assert.Equal(2, image.GetProperty("composite").GetProperty("version").GetInt32());
        Assert.Equal("sequential", image.GetProperty("composite").GetProperty("channelOrder").GetString());
        Assert.False(image.TryGetProperty("rendered", out _));

        stream.Position = 0;
        GbcAlbum roundTripped = GameBoyCameraCompatibility.LoadGbPrinterWebAlbum(stream, new GameBoyCameraLoadOptions
        {
            FrameMode = GameBoyCameraFrameMode.Keep,
        });

        GbcPhoto imported = Assert.Single(roundTripped.Photos);
        Assert.True(imported.HasRenderedImage);
        Assert.NotNull(imported.AverageData);
        Assert.Single(imported.AverageData!.SourceGroups);
        Assert.Equal(6, imported.AverageData.SourcePhotoCount);
    }

    [Fact]
    public async Task ExportGbPrinterWebJsonAsync_round_trips_direct_average_images_as_average_composites()
    {
        IReadOnlyList<GbcPhoto> sources =
        [
            CreateRenderedPhoto(new Rgba32(255, 0, 0)),
            CreateRenderedPhoto(new Rgba32(0, 255, 0)),
            CreateRenderedPhoto(new Rgba32(0, 0, 255)),
        ];

        GbcPhoto average = GameBoyCameraCompositionService.CreateDirectAveragePhoto(sources);
        Assert.NotNull(average.AverageData);
        Assert.Equal(GameBoyCameraAverageCompositionPipeline.Direct, average.AverageData!.Pipeline);

        GbcAlbum album = new(new GameBoyCameraAlbumMetadata(GameBoyCameraSourceKind.GbPrinterWebJson, null, 1, null, 1), [average]);

        await using MemoryStream stream = new();
        await GameBoyCameraCompatibility.ExportGbPrinterWebJsonAsync(album, stream, new GameBoyCameraJsonExportOptions
        {
            LastUpdateUtc = DateTimeOffset.UnixEpoch,
            TitleFactory = static (_, _) => "Average Composite",
            CreatedFactory = static (_, _) => "2026-04-15 00:00:00:000",
        });

        stream.Position = 0;
        using JsonDocument document = JsonDocument.Parse(stream);
        JsonElement image = Assert.Single(document.RootElement.GetProperty("state").GetProperty("images").EnumerateArray());
        Assert.Equal("average", image.GetProperty("type").GetString());
        Assert.Equal("direct", image.GetProperty("composite").GetProperty("pipeline").GetString());

        stream.Position = 0;
        GbcAlbum roundTripped = GameBoyCameraCompatibility.LoadGbPrinterWebAlbum(stream, new GameBoyCameraLoadOptions
        {
            FrameMode = GameBoyCameraFrameMode.Keep,
        });

        GbcPhoto imported = Assert.Single(roundTripped.Photos);
        Assert.NotNull(imported.AverageData);
        Assert.Equal(GameBoyCameraAverageCompositionPipeline.Direct, imported.AverageData!.Pipeline);
        Assert.Single(imported.AverageData.SourceGroups);
        Assert.Equal(3, imported.AverageData.SourcePhotoCount);
    }

    [Fact]
    public void CreateAveragePhotos_artifact_rgb_stack_uses_two_fifteen_photo_groups()
    {
        using FileStream stream = File.OpenRead(GetArtifactPath("20240812_095518_GBC_ADA1D77D_PHOTO_RGB_Pics.sav"));

        GbcAlbum album = GameBoyCameraCompatibility.LoadSaveAlbum(stream, new GameBoyCameraLoadOptions
        {
            FrameMode = GameBoyCameraFrameMode.Keep,
        });

        Assert.True(album.Photos.Count >= 30);

        GameBoyCameraAverageCompositionResult result = GameBoyCameraCompositionService.CreateAveragePhotos(
            album.Photos.Take(30).ToArray(),
            new GameBoyCameraAverageCompositionOptions(
                GameBoyCameraCompositionChannelOrder.Sequential,
                GameBoyCameraAverageCompositionMode.FullBank));

        Assert.Equal(new[] { 15, 15 }, result.SourceGroupSizes);
        Assert.Equal(10, result.RgbPhotos.Count);
        Assert.Equal(3, result.AveragePhotos.Count);
    }

    private static GbcPhoto CreateSolidPhoto(byte value)
    {
        using Image<Rgba32> image = new(
            GameBoyCameraConstants.RawPhotoTileWidth * GameBoyCameraConstants.TilePixelWidth,
            GameBoyCameraConstants.RawPhotoTileHeight * GameBoyCameraConstants.TilePixelHeight,
            new Rgba32(value, value, value));
        GbcTileGrid grid = GameBoyCameraImageCodec.EncodeToTileGrid(image, GameBoyCameraPalette.Default);
        return new GbcPhoto(grid, null, null, null);
    }

    private static GbcPhoto CreateRenderedPhoto(Rgba32 color)
    {
        using Image<Rgba32> image = new(
            GameBoyCameraConstants.RawPhotoTileWidth * GameBoyCameraConstants.TilePixelWidth,
            GameBoyCameraConstants.RawPhotoTileHeight * GameBoyCameraConstants.TilePixelHeight,
            color);
        GbcTileGrid grid = GameBoyCameraImageCodec.EncodeToTileGrid(image, GameBoyCameraPalette.Default);
        return new GbcPhoto(
            grid,
            null,
            null,
            new GbcThumbnail(GameBoyCameraImageCodec.CreateThumbnailBytes(image)),
            GameBoyCameraImageCodec.CopyPixelData(image),
            image.Width,
            image.Height);
    }

    private static string GetArtifactPath(string fileName)
        => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "GBTools.ImageSharp.GameBoyCamera.Tests", "TestArtifacts", fileName));
}