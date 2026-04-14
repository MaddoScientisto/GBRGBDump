using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;

namespace GBTools.ImageSharp.GameBoyCamera;

public sealed class GameBoyCameraEncoder : ImageEncoder
{
    protected override void Encode<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
        => throw new NotSupportedException("Game Boy Camera canonical encoding has not been implemented yet. Start with tile encoding, metadata emission, and compatibility exporters described in the plan.");
}