using GBTools.ImageSharp.GameBoyCamera.Model;

namespace GBTools.ImageSharp.GameBoyCamera.Compatibility;

public sealed class GameBoyCameraJsonExportOptions
{
    public DateTimeOffset LastUpdateUtc { get; init; } = DateTimeOffset.UtcNow;

    public Func<int, GbcPhoto, string>? TitleFactory { get; init; }

    public Func<int, GbcPhoto, string>? CreatedFactory { get; init; }

    public string PaletteName { get; init; } = "bw";

    public string FramePaletteName { get; init; } = "bw";
}