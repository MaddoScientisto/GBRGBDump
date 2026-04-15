namespace GBTools.ImageSharp.GameBoyCamera.Model;

public sealed record GbcFrameOverlay(
    IReadOnlyList<GbcTile2Bpp> Upper,
    IReadOnlyList<IReadOnlyList<GbcTile2Bpp>> Left,
    IReadOnlyList<IReadOnlyList<GbcTile2Bpp>> Right,
    IReadOnlyList<GbcTile2Bpp> Lower);