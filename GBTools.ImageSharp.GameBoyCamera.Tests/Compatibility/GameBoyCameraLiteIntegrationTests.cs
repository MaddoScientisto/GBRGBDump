using GBTools.ImageSharp.GameBoyCamera.Lite;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace GBTools.ImageSharp.GameBoyCamera.Tests.Compatibility;

public class GameBoyCameraLiteIntegrationTests
{
    [Fact]
    public async Task Decode_raw_bin_fixture_and_write_png_to_temp_folder()
    {
        string artifactPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "TestArtifacts", "photo-01.gbci-01_test.bin"));
        Assert.True(File.Exists(artifactPath), $"Expected test artifact at '{artifactPath}'.");

        byte[] binBytes = await File.ReadAllBytesAsync(artifactPath);

        using Image<Rgba32> image = GameBoyCameraBinDecoder.Decode(binBytes);

        string outputDirectory = Path.Combine(Path.GetTempPath(), "GBTools.ImageSharp.GameBoyCamera.Lite.Tests");
        Directory.CreateDirectory(outputDirectory);

        string outputPath = Path.Combine(outputDirectory, $"{Path.GetFileNameWithoutExtension(artifactPath)}.png");
        await image.SaveAsPngAsync(outputPath);

        Assert.Equal(160, image.Width);
        Assert.Equal(144, image.Height);
        Assert.True(File.Exists(outputPath), $"Expected PNG output at '{outputPath}'.");

        FileInfo fileInfo = new(outputPath);
        Assert.True(fileInfo.Length > 0, $"Expected non-empty PNG output at '{outputPath}'.");
    }
}