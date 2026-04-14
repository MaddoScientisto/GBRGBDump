using GBTools.ImageSharp.GameBoyCamera.Codec;
using GBTools.ImageSharp.GameBoyCamera.Model;

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

    [Fact]
    public void StripFrame_and_applyFrame_round_trip_full_grid()
    {
        string[] rawTiles = Enumerable.Range(0, 360)
            .Select(index => string.Join(" ", Enumerable.Repeat((index % 256).ToString("X2"), 16)))
            .ToArray();
        var framedGrid = GameBoyCameraTileGridFactory.CreateFromTextTiles(rawTiles, GameBoyCameraConstants.FramedPhotoTileWidth);

        GbcFrameOverlay overlay = GameBoyCameraFrameCodec.ExtractFrameOverlay(framedGrid, 2);
        var stripped = GameBoyCameraFrameCodec.StripFrame(framedGrid, 2);
        var reapplied = GameBoyCameraFrameCodec.ApplyFrame(stripped, overlay, 2);

        Assert.Equal(GameBoyCameraConstants.RawPhotoTileWidth, stripped.WidthInTiles);
        Assert.Equal(GameBoyCameraConstants.RawPhotoTileHeight, stripped.HeightInTiles);
        Assert.Equal(GameBoyCameraConstants.FramedPhotoTileWidth, reapplied.WidthInTiles);
        Assert.Equal(GameBoyCameraConstants.FramedPhotoTileHeight, reapplied.HeightInTiles);
        Assert.Equal(
            rawTiles,
            reapplied.Tiles.Select(GameBoyCameraTileTextCodec.FormatTile).ToArray());
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(39, true)]
    [InlineData(42, false)]
    [InlineData(44, false)]
    [InlineData(319, true)]
    [InlineData(337, true)]
    public void IsFrameTileIndex_matches_frame_layout(int index, bool expected)
    {
        Assert.Equal(expected, GameBoyCameraFrameCodec.IsFrameTileIndex(index, 2));
    }

    [Fact]
    public void SerializeFrameOverlay_and_parseFrameOverlay_round_trip_json_payload()
    {
        string[] rawTiles = Enumerable.Range(0, 360)
            .Select(index => string.Join(" ", Enumerable.Repeat((index % 256).ToString("X2"), 16)))
            .ToArray();
        var grid = GameBoyCameraTileGridFactory.CreateFromTextTiles(rawTiles, GameBoyCameraConstants.FramedPhotoTileWidth);
        GbcFrameOverlay overlay = GameBoyCameraFrameCodec.ExtractFrameOverlay(grid, 2);

        string payload = GameBoyCameraFrameCodec.SerializeFrameOverlay(overlay);
        GbcFrameOverlay parsed = GameBoyCameraFrameCodec.ParseFrameOverlay(payload);

        Assert.Equal(
            overlay.Upper.Select(GameBoyCameraTileTextCodec.FormatTile),
            parsed.Upper.Select(GameBoyCameraTileTextCodec.FormatTile));
        Assert.Equal(
            overlay.Lower.Select(GameBoyCameraTileTextCodec.FormatTile),
            parsed.Lower.Select(GameBoyCameraTileTextCodec.FormatTile));
        Assert.Equal(
            overlay.Left.SelectMany(static row => row).Select(GameBoyCameraTileTextCodec.FormatTile),
            parsed.Left.SelectMany(static row => row).Select(GameBoyCameraTileTextCodec.FormatTile));
        Assert.Equal(
            overlay.Right.SelectMany(static row => row).Select(GameBoyCameraTileTextCodec.FormatTile),
            parsed.Right.SelectMany(static row => row).Select(GameBoyCameraTileTextCodec.FormatTile));
    }

    [Fact]
    public void TryParseFrameOverlay_supports_legacy_tile_list_payload()
    {
        string legacyPayload = string.Join(Environment.NewLine, Enumerable.Range(0, 136)
            .Select(index => string.Join(" ", Enumerable.Repeat((index % 256).ToString("X2"), 16))));

        bool parsed = GameBoyCameraFrameCodec.TryParseFrameOverlay(legacyPayload, out GbcFrameOverlay? overlay);

        Assert.True(parsed);
        Assert.NotNull(overlay);
        Assert.Equal(40, overlay!.Upper.Count);
        Assert.Equal(14, overlay.Left.Count);
        Assert.Equal(14, overlay.Right.Count);
        Assert.Equal(40, overlay.Lower.Count);
        Assert.Equal("00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00", GameBoyCameraTileTextCodec.FormatTile(overlay.Upper[0]));
        Assert.Equal("60 60 60 60 60 60 60 60 60 60 60 60 60 60 60 60", GameBoyCameraTileTextCodec.FormatTile(overlay.Lower[0]));
    }

    [Fact]
    public void MapCartFrameToHash_follows_reference_fallback_order()
    {
        IReadOnlyList<GbcFrameReference> frames =
        [
            new("jp01", "jp-first"),
            new("int03", "int-third"),
            new("custom01", "custom-first"),
            new("int01", "int-first"),
        ];

        Assert.Equal("int-third", GameBoyCameraFrameCodec.MapCartFrameToHash(2, "jp", frames));
        Assert.Equal("custom-first", GameBoyCameraFrameCodec.MapCartFrameToHash(9, "custom", frames));
        Assert.Equal("int-first", GameBoyCameraFrameCodec.MapCartFrameToHash(9, "missing", frames));
    }
}