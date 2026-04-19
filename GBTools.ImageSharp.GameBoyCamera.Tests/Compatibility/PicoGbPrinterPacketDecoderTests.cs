using GBTools.ImageSharp.GameBoyCamera.Lite;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace GBTools.ImageSharp.GameBoyCamera.Tests.Compatibility;

public class PicoGbPrinterPacketDecoderTests
{
    [Fact]
    public async Task Decode_pico_dump_and_write_png_to_temp_folder()
    {
        string artifactPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "TestArtifacts", "pico_77401200.bin"));
        Assert.True(File.Exists(artifactPath), $"Expected test artifact at '{artifactPath}'.");

        byte[] bytes = await File.ReadAllBytesAsync(artifactPath);
        Assert.True(PicoGbPrinterPacketDecoder.LooksLikePicoStream(bytes),
            "Fixture should be detected as a Pico GB Printer packet stream.");

        IReadOnlyList<Image<Rgba32>> images = PicoGbPrinterPacketDecoder.DecodeAll(bytes);
        Assert.NotEmpty(images);

        string outputDirectory = Path.Combine(Path.GetTempPath(), "GBTools.ImageSharp.GameBoyCamera.Lite.Tests");
        Directory.CreateDirectory(outputDirectory);

        string stem = Path.GetFileNameWithoutExtension(artifactPath);
        for (int i = 0; i < images.Count; i++)
        {
            string outputPath = Path.Combine(outputDirectory, $"{stem}_{i:00}.png");
            await images[i].SaveAsPngAsync(outputPath);
            FileInfo info = new(outputPath);
            Assert.True(info.Length > 0, $"Expected non-empty PNG output at '{outputPath}'.");
        }

        foreach (Image<Rgba32> image in images)
        {
            image.Dispose();
        }
    }

    [Theory]
    [InlineData("pico_6730200.bin")]
    [InlineData("pico_6723200.bin")]
    [InlineData("pico_11800200.bin")]
    [InlineData("pico_16649200.bin")]
    [InlineData("pico_21371200.bin")]
    [InlineData("pico_24045200.bin")]
    public async Task Decode_real_cardputer_capture_writes_png(string fileName)
    {
        string artifactPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "TestArtifacts", "picogbprinter", fileName));
        Assert.True(File.Exists(artifactPath), $"Expected test artifact at '{artifactPath}'.");

        byte[] bytes = await File.ReadAllBytesAsync(artifactPath);
        Assert.True(PicoGbPrinterPacketDecoder.LooksLikePicoStream(bytes),
            $"{fileName} should be detected as a Pico GB Printer packet stream.");

        IReadOnlyList<Image<Rgba32>> images = PicoGbPrinterPacketDecoder.DecodeAll(bytes);
        Assert.NotEmpty(images);

        string outputDirectory = Path.Combine(Path.GetTempPath(), "GBTools.ImageSharp.GameBoyCamera.Lite.Tests", "picogbprinter");
        Directory.CreateDirectory(outputDirectory);

        string stem = Path.GetFileNameWithoutExtension(artifactPath);
        for (int i = 0; i < images.Count; i++)
        {
            string outputPath = Path.Combine(outputDirectory, $"{stem}_{i:00}.png");
            await images[i].SaveAsPngAsync(outputPath);
            FileInfo info = new(outputPath);
            Assert.True(info.Length > 0, $"Expected non-empty PNG output at '{outputPath}'.");
        }

        foreach (Image<Rgba32> image in images)
        {
            image.Dispose();
        }
    }
}
