namespace GBTools.ImageSharp.GameBoyCamera;

using GBTools.ImageSharp.GameBoyCamera.Codec;

public sealed class GameBoyCameraLoadOptions
{
    public GameBoyCameraFrameMode FrameMode { get; init; } = GameBoyCameraFrameMode.Keep;

    public GameBoyCameraSourceKind SourceKind { get; init; } = GameBoyCameraSourceKind.Canonical;

    public GameBoyCameraPalette Palette { get; init; } = GameBoyCameraPalette.Default;

    public bool ReturnMultiFrameImage { get; init; } = true;

    public bool IncludeDeleted { get; init; }

    public bool IncludeLastSeen { get; init; } = true;

    public bool ForceMagicCheck { get; init; } = true;
}