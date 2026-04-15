using GBTools.ImageSharp.GameBoyCamera;
using SixLabors.ImageSharp;

namespace GBTools.ImageSharp.GameBoyCamera.Tests.ConfigurationTests;

public class GameBoyCameraConfigurationModuleTests
{
    [Fact]
    public void Configure_registers_format_decoder_and_encoder()
    {
        Configuration configuration = Configuration.Default.Clone();
        GameBoyCameraConfigurationModule module = new();

        module.Configure(configuration);

        bool found = configuration.ImageFormatsManager.TryFindFormatByFileExtension(GameBoyCameraConstants.DefaultFileExtension, out var format);

        Assert.True(found);
        Assert.NotNull(format);
        Assert.IsType<GameBoyCameraDecoder>(configuration.ImageFormatsManager.GetDecoder(format!));
        Assert.IsType<GameBoyCameraEncoder>(configuration.ImageFormatsManager.GetEncoder(format!));
    }
}