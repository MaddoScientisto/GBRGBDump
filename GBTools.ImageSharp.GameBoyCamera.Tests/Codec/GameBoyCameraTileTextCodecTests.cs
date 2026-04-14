using GBTools.ImageSharp.GameBoyCamera.Codec;

namespace GBTools.ImageSharp.GameBoyCamera.Tests.Codec;

public class GameBoyCameraTileTextCodecTests
{
    [Fact]
    public void Parse_and_format_tile_round_trips_text_representation()
    {
        const string rawTile = "00 11 22 33 44 55 66 77 88 99 AA BB CC DD EE FF";

        var tile = GameBoyCameraTileTextCodec.ParseTile(rawTile);
        string formatted = GameBoyCameraTileTextCodec.FormatTile(tile);

        Assert.Equal(rawTile, formatted);
    }

    [Fact]
    public void TryParseTile_rejects_incorrect_length()
    {
        bool parsed = GameBoyCameraTileTextCodec.TryParseTile("00 11", out var tile);

        Assert.False(parsed);
        Assert.Null(tile);
    }
}