using SixLabors.ImageSharp.Formats;

namespace GBTools.ImageSharp.GameBoyCamera;

public sealed class GameBoyCameraFormat : IImageFormat
{
    public static GameBoyCameraFormat Instance { get; } = new();

    public string Name => GameBoyCameraConstants.FormatName;

    public string DefaultMimeType => GameBoyCameraConstants.DefaultMimeType;

    public IEnumerable<string> MimeTypes => [GameBoyCameraConstants.DefaultMimeType];

    public IEnumerable<string> FileExtensions => [GameBoyCameraConstants.DefaultFileExtension];
}