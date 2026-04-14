using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;

namespace GBTools.ImageSharp.GameBoyCamera;

public sealed class GameBoyCameraDecoder : ImageDecoder
{
    protected override Image Decode(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
        => throw new NotSupportedException("Game Boy Camera canonical decoding has not been implemented yet. Start with the codec model and compatibility adapters described in the plan.");

    protected override Image<TPixel> Decode<TPixel>(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
        => throw new NotSupportedException("Game Boy Camera canonical decoding has not been implemented yet. Start with the codec model and compatibility adapters described in the plan.");

    protected override ImageInfo Identify(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
        => throw new NotSupportedException("Game Boy Camera image identification has not been implemented yet.");
}