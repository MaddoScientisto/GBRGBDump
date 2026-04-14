using GBTools.ImageSharp.GameBoyCamera;

namespace GBTools.ImageSharp.GameBoyCamera.Tests.Formats;

public class GameBoyCameraFormatTests
{
    [Fact]
    public void Format_exposes_expected_identity_values()
    {
        GameBoyCameraFormat format = GameBoyCameraFormat.Instance;

        Assert.Equal(GameBoyCameraConstants.FormatName, format.Name);
        Assert.Equal(GameBoyCameraConstants.DefaultMimeType, format.DefaultMimeType);
        Assert.Contains(GameBoyCameraConstants.DefaultMimeType, format.MimeTypes);
        Assert.Contains(GameBoyCameraConstants.DefaultFileExtension, format.FileExtensions);
    }

    [Fact]
    public void Detector_matches_canonical_header()
    {
        GameBoyCameraFormatDetector detector = new();
        byte[] header = GameBoyCameraConstants.CanonicalMagicHeader.ToArray();

        bool detected = detector.TryDetectFormat(header, out var format);

        Assert.True(detected);
        Assert.Same(GameBoyCameraFormat.Instance, format);
    }
}