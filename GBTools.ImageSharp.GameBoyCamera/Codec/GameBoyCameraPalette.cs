using SixLabors.ImageSharp.PixelFormats;

namespace GBTools.ImageSharp.GameBoyCamera.Codec;

public sealed class GameBoyCameraPalette
{
    public static GameBoyCameraPalette Default { get; } = new(
        new Rgba32(255, 255, 255, 255),
        new Rgba32(170, 170, 170, 255),
        new Rgba32(85, 85, 85, 255),
        new Rgba32(0, 0, 0, 255));

    public GameBoyCameraPalette(Rgba32 color0, Rgba32 color1, Rgba32 color2, Rgba32 color3)
    {
        Colors = [color0, color1, color2, color3];
    }

    public IReadOnlyList<Rgba32> Colors { get; }

    public Rgba32 this[int index] => Colors[index];
}