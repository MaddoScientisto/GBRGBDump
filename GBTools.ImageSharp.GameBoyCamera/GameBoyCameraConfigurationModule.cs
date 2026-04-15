using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;

namespace GBTools.ImageSharp.GameBoyCamera;

public sealed class GameBoyCameraConfigurationModule : IImageFormatConfigurationModule
{
    public void Configure(Configuration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        ImageFormatManager manager = configuration.ImageFormatsManager;

        if (!manager.ImageFormats.Any(static format => string.Equals(format.Name, GameBoyCameraConstants.FormatName, StringComparison.Ordinal)))
        {
            manager.AddImageFormat(GameBoyCameraFormat.Instance);
        }

        manager.AddImageFormatDetector(new GameBoyCameraFormatDetector());
        manager.SetDecoder(GameBoyCameraFormat.Instance, new GameBoyCameraDecoder());
        manager.SetEncoder(GameBoyCameraFormat.Instance, new GameBoyCameraEncoder());
    }
}