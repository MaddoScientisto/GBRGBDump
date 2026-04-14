using GBTools.ImageSharp.GameBoyCamera.Codec;

namespace GBTools.ImageSharp.GameBoyCamera.Tests.Codec;

public class GameBoyCameraTileCodecTests
{
    [Fact]
    public void DecodeToColorIndexes_decodes_known_2bpp_row_pattern()
    {
        var tile = GameBoyCameraTileTextCodec.ParseTile("55 33 00 00 00 00 00 00 00 00 00 00 00 00 00 00");

        byte[] pixels = GameBoyCameraTileCodec.DecodeToColorIndexes(tile);

        Assert.Equal([0, 1, 2, 3, 0, 1, 2, 3], pixels.Take(8).ToArray());
    }

    [Fact]
    public void EncodeFromColorIndexes_round_trips_known_pattern()
    {
        byte[] pixels = [
            0, 1, 2, 3, 0, 1, 2, 3,
            0, 1, 2, 3, 0, 1, 2, 3,
            0, 1, 2, 3, 0, 1, 2, 3,
            0, 1, 2, 3, 0, 1, 2, 3,
            0, 1, 2, 3, 0, 1, 2, 3,
            0, 1, 2, 3, 0, 1, 2, 3,
            0, 1, 2, 3, 0, 1, 2, 3,
            0, 1, 2, 3, 0, 1, 2, 3,
        ];

        var tile = GameBoyCameraTileCodec.EncodeFromColorIndexes(pixels);
        byte[] decoded = GameBoyCameraTileCodec.DecodeToColorIndexes(tile);

        Assert.Equal(pixels, decoded);
    }
}