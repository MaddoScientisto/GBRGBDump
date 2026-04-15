using SixLabors.ImageSharp.Formats;

namespace GBTools.ImageSharp.GameBoyCamera;

public sealed class GameBoyCameraFormatDetector : IImageFormatDetector
{
    public int HeaderSize => GameBoyCameraConstants.CanonicalMagicHeader.Length;

    public bool TryDetectFormat(ReadOnlySpan<byte> header, out IImageFormat format)
    {
        if (header.Length >= HeaderSize && header[..HeaderSize].SequenceEqual(GameBoyCameraConstants.CanonicalMagicHeader))
        {
            format = GameBoyCameraFormat.Instance;
            return true;
        }

        format = default!;
        return false;
    }
}