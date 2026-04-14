using GBTools.ImageSharp.GameBoyCamera.Codec;

namespace GBTools.ImageSharp.GameBoyCamera.Tests.Codec;

public class GameBoyCameraRawPhotoTransformTests
{
    [Fact]
    public void TryTransformFramedPhoto_returns_false_for_blank_white_photo()
    {
        byte[] bytes = Enumerable.Repeat((byte)0x00, 0x0E00).ToArray();

        bool transformed = GameBoyCameraRawPhotoTransform.TryTransformFramedPhoto(bytes, 0, out var grid);

        Assert.False(transformed);
        Assert.Null(grid);
    }

    [Fact]
    public void TryTransformFramedPhoto_builds_20x18_grid_for_non_blank_photo()
    {
        byte[] bytes = Enumerable.Repeat((byte)0x00, 0x0E00).ToArray();
        bytes[0] = 0xFF;
        bytes[1] = 0xAA;

        bool transformed = GameBoyCameraRawPhotoTransform.TryTransformFramedPhoto(bytes, 0, out var grid);

        Assert.True(transformed);
        Assert.NotNull(grid);
        Assert.Equal(GameBoyCameraConstants.FramedPhotoTileWidth, grid!.WidthInTiles);
        Assert.Equal(GameBoyCameraConstants.FramedPhotoTileHeight, grid.HeightInTiles);
        Assert.Equal(GameBoyCameraConstants.FramedPhotoTileWidth * GameBoyCameraConstants.FramedPhotoTileHeight, grid.Tiles.Count);
    }
}