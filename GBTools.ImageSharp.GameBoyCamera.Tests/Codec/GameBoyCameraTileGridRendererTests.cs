using GBTools.ImageSharp.GameBoyCamera.Codec;

namespace GBTools.ImageSharp.GameBoyCamera.Tests.Codec;

public class GameBoyCameraTileGridRendererTests
{
    [Fact]
    public void Render_creates_expected_dimensions_and_palette_colors()
    {
        var tile = GameBoyCameraTileCodec.EncodeFromColorIndexes([
            0, 1, 2, 3, 0, 1, 2, 3,
            0, 1, 2, 3, 0, 1, 2, 3,
            0, 1, 2, 3, 0, 1, 2, 3,
            0, 1, 2, 3, 0, 1, 2, 3,
            0, 1, 2, 3, 0, 1, 2, 3,
            0, 1, 2, 3, 0, 1, 2, 3,
            0, 1, 2, 3, 0, 1, 2, 3,
            0, 1, 2, 3, 0, 1, 2, 3,
        ]);
        var grid = new Model.GbcTileGrid(1, 1, [tile]);

        using var image = GameBoyCameraTileGridRenderer.Render(grid);

        Assert.Equal(8, image.Width);
        Assert.Equal(8, image.Height);
        Assert.Equal(GameBoyCameraPalette.Default[0], image[0, 0]);
        Assert.Equal(GameBoyCameraPalette.Default[1], image[1, 0]);
        Assert.Equal(GameBoyCameraPalette.Default[2], image[2, 0]);
        Assert.Equal(GameBoyCameraPalette.Default[3], image[3, 0]);
    }
}