using GBTools.ImageSharp.GameBoyCamera.Composition;

namespace GBTools.ImageSharp.GameBoyCamera.Avalonia.Models;

public sealed record RgbCompositionRequest(
    GameBoyCameraCompositionChannelOrder ChannelOrder);

public sealed record DirectAverageCompositionRequest();

public sealed record SmartAverageCompositionRequest(
    GameBoyCameraCompositionChannelOrder ChannelOrder,
    GameBoyCameraAverageCompositionMode Mode);