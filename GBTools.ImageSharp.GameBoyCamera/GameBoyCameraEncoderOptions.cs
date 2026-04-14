namespace GBTools.ImageSharp.GameBoyCamera;

using GBTools.ImageSharp.GameBoyCamera.Codec;

public sealed class GameBoyCameraEncoderOptions
{
    public GameBoyCameraPalette Palette { get; init; } = GameBoyCameraPalette.Default;

    public GameBoyCameraSourceKind TargetKind { get; init; } = GameBoyCameraSourceKind.Canonical;

    public bool IncludeFrameData { get; init; } = true;

    public bool IncludeThumbnails { get; init; } = true;

    public bool PreserveSourceMetadata { get; init; } = true;
}