using GBTools.ImageSharp.GameBoyCamera.Codec;

namespace GBTools.ImageSharp.GameBoyCamera.Tests.Codec;

public class GameBoyCameraFrameCodecTests
{
    [Fact]
    public void ExtractFrameOverlay_returns_expected_section_sizes()
    {
        string[] rawTiles = Enumerable.Range(0, 360)
            .Select(index => string.Join(" ", Enumerable.Repeat((index % 256).ToString("X2"), 16)))
            .ToArray();
        var grid = GameBoyCameraTileGridFactory.CreateFromTextTiles(rawTiles, GameBoyCameraConstants.FramedPhotoTileWidth);

        var frame = GameBoyCameraFrameCodec.ExtractFrameOverlay(grid, 2);

        Assert.Equal(40, frame.Upper.Count);
        Assert.Equal(14, frame.Left.Count);
        Assert.Equal(14, frame.Right.Count);
        Assert.All(frame.Left, static row => Assert.Equal(2, row.Count));
        Assert.All(frame.Right, static row => Assert.Equal(2, row.Count));
        Assert.Equal(40, frame.Lower.Count);
    }
}