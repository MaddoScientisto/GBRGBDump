using GBTools.ImageSharp.GameBoyCamera.Codec;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;

namespace GBTools.ImageSharp.GameBoyCamera.Tests.Encode;

public class GameBoyCameraCanonicalCodecTests
{
    [Fact]
    public void Encoder_and_decoder_round_trip_canonical_photo()
    {
        using Image<Rgba32> image = CreateRenderedImage(GameBoyCameraConstants.RawPhotoTileWidth, GameBoyCameraConstants.RawPhotoTileHeight);
        using MemoryStream stream = new();

        image.Save(stream, new GameBoyCameraEncoder());
        stream.Position = 0;

        Configuration configuration = Configuration.Default.Clone();
        configuration.Configure(new GameBoyCameraConfigurationModule());
        DecoderOptions options = new()
        {
            Configuration = configuration,
        };

        using Image<Rgba32> decoded = Image.Load<Rgba32>(options, stream);

        Assert.Equal(image.Width, decoded.Width);
        Assert.Equal(image.Height, decoded.Height);
        Assert.Equal(image[0, 0], decoded[0, 0]);
    }

    [Fact]
    public void Encoder_supports_framed_images()
    {
        using Image<Rgba32> image = CreateRenderedImage(GameBoyCameraConstants.FramedPhotoTileWidth, GameBoyCameraConstants.FramedPhotoTileHeight);
        using MemoryStream stream = new();

        image.Save(stream, new GameBoyCameraEncoder());
        stream.Position = 0;

        Configuration configuration = Configuration.Default.Clone();
        configuration.Configure(new GameBoyCameraConfigurationModule());
        DecoderOptions options = new()
        {
            Configuration = configuration,
        };

        using Image<Rgba32> decoded = Image.Load<Rgba32>(options, stream);

        Assert.Equal(160, decoded.Width);
        Assert.Equal(144, decoded.Height);
    }

    private static Image<Rgba32> CreateRenderedImage(int widthInTiles, int heightInTiles)
    {
        string[] rawTiles = Enumerable.Range(0, widthInTiles * heightInTiles)
            .Select(index => string.Join(" ", Enumerable.Repeat((index % 256).ToString("X2"), 16)))
            .ToArray();
        return GameBoyCameraTileGridRenderer.Render(GameBoyCameraTileGridFactory.CreateFromTextTiles(rawTiles, widthInTiles));
    }
}