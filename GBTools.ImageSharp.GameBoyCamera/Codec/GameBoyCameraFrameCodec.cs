using System.Text.Json;
using GBTools.ImageSharp.GameBoyCamera.Model;

namespace GBTools.ImageSharp.GameBoyCamera.Codec;

public static class GameBoyCameraFrameCodec
{
    private static readonly JsonSerializerOptions FrameJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static GbcFrameOverlay ExtractFrameOverlay(GbcTileGrid grid, int imageStartLine)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentOutOfRangeException.ThrowIfNegative(imageStartLine);

        if (grid.WidthInTiles != GameBoyCameraConstants.FramedPhotoTileWidth)
        {
            throw new ArgumentException("Frame extraction expects a 20-tile-wide grid.", nameof(grid));
        }

        if (grid.HeightInTiles < imageStartLine + GameBoyCameraConstants.RawPhotoTileHeight)
        {
            throw new ArgumentException("Grid height is too small for the requested frame extraction start line.", nameof(grid));
        }

        IReadOnlyList<GbcTile2Bpp> upper = grid.Tiles.Take(imageStartLine * grid.WidthInTiles).ToArray();
        IReadOnlyList<GbcTile2Bpp> lower = grid.Tiles.Skip((imageStartLine + GameBoyCameraConstants.RawPhotoTileHeight) * grid.WidthInTiles).ToArray();

        IReadOnlyList<IReadOnlyList<GbcTile2Bpp>> left = Enumerable.Range(0, GameBoyCameraConstants.RawPhotoTileHeight)
            .Select(line => (IReadOnlyList<GbcTile2Bpp>)grid.Tiles.Skip((line + imageStartLine) * grid.WidthInTiles).Take(2).ToArray())
            .ToArray();

        IReadOnlyList<IReadOnlyList<GbcTile2Bpp>> right = Enumerable.Range(0, GameBoyCameraConstants.RawPhotoTileHeight)
            .Select(line => (IReadOnlyList<GbcTile2Bpp>)grid.Tiles.Skip(((line + imageStartLine) * grid.WidthInTiles) + 18).Take(2).ToArray())
            .ToArray();

        return new GbcFrameOverlay(upper, left, right, lower);
    }

    public static bool IsFrameTileIndex(int tileIndex, int imageStartLine, int widthInTiles = GameBoyCameraConstants.FramedPhotoTileWidth)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tileIndex);
        ArgumentOutOfRangeException.ThrowIfNegative(imageStartLine);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(widthInTiles);

        int line = tileIndex / widthInTiles;
        int column = tileIndex % widthInTiles;
        return line < imageStartLine
            || line >= imageStartLine + GameBoyCameraConstants.RawPhotoTileHeight
            || column < 2
            || column >= widthInTiles - 2;
    }

    public static GbcTileGrid StripFrame(GbcTileGrid grid, int imageStartLine)
    {
        ArgumentNullException.ThrowIfNull(grid);

        if (grid.WidthInTiles != GameBoyCameraConstants.FramedPhotoTileWidth)
        {
            throw new ArgumentException("Frame stripping expects a 20-tile-wide grid.", nameof(grid));
        }

        List<GbcTile2Bpp> tiles = new(GameBoyCameraConstants.RawPhotoTileWidth * GameBoyCameraConstants.RawPhotoTileHeight);
        for (int line = 0; line < GameBoyCameraConstants.RawPhotoTileHeight; line++)
        {
            int offset = ((line + imageStartLine) * grid.WidthInTiles) + 2;
            tiles.AddRange(grid.Tiles.Skip(offset).Take(GameBoyCameraConstants.RawPhotoTileWidth));
        }

        return new GbcTileGrid(GameBoyCameraConstants.RawPhotoTileWidth, GameBoyCameraConstants.RawPhotoTileHeight, tiles);
    }

    public static GbcTileGrid ApplyFrame(GbcTileGrid grid, GbcFrameOverlay frameOverlay, int imageStartLine)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(frameOverlay);

        if (grid.WidthInTiles != GameBoyCameraConstants.RawPhotoTileWidth || grid.HeightInTiles != GameBoyCameraConstants.RawPhotoTileHeight)
        {
            throw new ArgumentException("Frame application expects a 16x14 raw photo grid.", nameof(grid));
        }

        ValidateFrameOverlay(frameOverlay, imageStartLine);

        List<GbcTile2Bpp> result = new(GameBoyCameraConstants.FramedPhotoTileWidth * GameBoyCameraConstants.FramedPhotoTileHeight);
        result.AddRange(frameOverlay.Upper);

        for (int line = 0; line < GameBoyCameraConstants.RawPhotoTileHeight; line++)
        {
            result.AddRange(frameOverlay.Left[line]);
            result.AddRange(grid.Tiles.Skip(line * GameBoyCameraConstants.RawPhotoTileWidth).Take(GameBoyCameraConstants.RawPhotoTileWidth));
            result.AddRange(frameOverlay.Right[line]);
        }

        result.AddRange(frameOverlay.Lower);
        return new GbcTileGrid(GameBoyCameraConstants.FramedPhotoTileWidth, GameBoyCameraConstants.FramedPhotoTileHeight, result);
    }

    public static string SerializeFrameOverlay(GbcFrameOverlay frameOverlay)
    {
        ArgumentNullException.ThrowIfNull(frameOverlay);
        ValidateFrameOverlay(frameOverlay, imageStartLine: 2);

        var payload = new FrameOverlayPayload(
            frameOverlay.Upper.Select(GameBoyCameraTileTextCodec.FormatTile).ToArray(),
            frameOverlay.Left.Select(static row => row.Select(GameBoyCameraTileTextCodec.FormatTile).ToArray()).ToArray(),
            frameOverlay.Right.Select(static row => row.Select(GameBoyCameraTileTextCodec.FormatTile).ToArray()).ToArray(),
            frameOverlay.Lower.Select(GameBoyCameraTileTextCodec.FormatTile).ToArray());

        return JsonSerializer.Serialize(payload, FrameJsonOptions);
    }

    public static bool TryParseFrameOverlay(string payload, out GbcFrameOverlay? frameOverlay)
    {
        frameOverlay = null;
        if (string.IsNullOrWhiteSpace(payload))
        {
            return false;
        }

        if (TryParseJsonPayload(payload, out frameOverlay))
        {
            return true;
        }

        return TryParseLegacyPayload(payload, out frameOverlay);
    }

    public static GbcFrameOverlay ParseFrameOverlay(string payload)
    {
        if (!TryParseFrameOverlay(payload, out GbcFrameOverlay? frameOverlay) || frameOverlay is null)
        {
            throw new FormatException("Expected a JSON or legacy tile-list frame payload.");
        }

        return frameOverlay;
    }

    public static string MapCartFrameToHash(int frameNumber, string? frameType, IReadOnlyList<GbcFrameReference> frames)
    {
        ArgumentNullException.ThrowIfNull(frames);

        if (string.IsNullOrWhiteSpace(frameType))
        {
            return string.Empty;
        }

        string paddedFrameNumber = (frameNumber + 1).ToString("D2", System.Globalization.CultureInfo.InvariantCulture);

        string? exact = FindHash($"{frameType}{paddedFrameNumber}", frames);
        if (!string.IsNullOrEmpty(exact))
        {
            return exact;
        }

        if (string.Equals(frameType, "jp", StringComparison.OrdinalIgnoreCase))
        {
            string? japaneseFallback = FindHash($"int{paddedFrameNumber}", frames);
            if (!string.IsNullOrEmpty(japaneseFallback))
            {
                return japaneseFallback;
            }
        }

        string? firstFrameFallback = FindHash($"{frameType}01", frames);
        if (!string.IsNullOrEmpty(firstFrameFallback))
        {
            return firstFrameFallback;
        }

        if (!string.Equals(frameType, "jp", StringComparison.OrdinalIgnoreCase))
        {
            string? internationalFallback = FindHash($"int{paddedFrameNumber}", frames);
            if (!string.IsNullOrEmpty(internationalFallback))
            {
                return internationalFallback;
            }
        }

        return FindHash("int01", frames) ?? string.Empty;
    }

    private static void ValidateFrameOverlay(GbcFrameOverlay frameOverlay, int imageStartLine)
    {
        if (frameOverlay.Upper.Count != imageStartLine * GameBoyCameraConstants.FramedPhotoTileWidth)
        {
            throw new ArgumentException("Frame overlay upper section length is invalid.", nameof(frameOverlay));
        }

        if (frameOverlay.Left.Count != GameBoyCameraConstants.RawPhotoTileHeight || frameOverlay.Right.Count != GameBoyCameraConstants.RawPhotoTileHeight)
        {
            throw new ArgumentException("Frame overlay side sections must contain 14 rows.", nameof(frameOverlay));
        }

        if (frameOverlay.Left.Any(static row => row.Count != 2) || frameOverlay.Right.Any(static row => row.Count != 2))
        {
            throw new ArgumentException("Frame overlay side rows must contain 2 tiles.", nameof(frameOverlay));
        }

        int expectedLower = (GameBoyCameraConstants.FramedPhotoTileHeight - imageStartLine - GameBoyCameraConstants.RawPhotoTileHeight) * GameBoyCameraConstants.FramedPhotoTileWidth;
        if (frameOverlay.Lower.Count != expectedLower)
        {
            throw new ArgumentException("Frame overlay lower section length is invalid.", nameof(frameOverlay));
        }
    }

    private static bool TryParseJsonPayload(string payload, out GbcFrameOverlay? frameOverlay)
    {
        frameOverlay = null;

        try
        {
            FrameOverlayPayload? data = JsonSerializer.Deserialize<FrameOverlayPayload>(payload, FrameJsonOptions);
            if (data is null)
            {
                return false;
            }

            frameOverlay = new GbcFrameOverlay(
                GameBoyCameraTileTextCodec.ParseTiles(data.Upper ?? []).ToArray(),
                (data.Left ?? []).Select(static row => (IReadOnlyList<GbcTile2Bpp>)GameBoyCameraTileTextCodec.ParseTiles(row ?? []).ToArray()).ToArray(),
                (data.Right ?? []).Select(static row => (IReadOnlyList<GbcTile2Bpp>)GameBoyCameraTileTextCodec.ParseTiles(row ?? []).ToArray()).ToArray(),
                GameBoyCameraTileTextCodec.ParseTiles(data.Lower ?? []).ToArray());

            ValidateFrameOverlay(frameOverlay, imageStartLine: 2);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryParseLegacyPayload(string payload, out GbcFrameOverlay? frameOverlay)
    {
        frameOverlay = null;

        try
        {
            string[] tiles = payload
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (tiles.Length != 136)
            {
                return false;
            }

            frameOverlay = new GbcFrameOverlay(
                GameBoyCameraTileTextCodec.ParseTiles(tiles.Take(40)).ToArray(),
                Enumerable.Range(0, 14)
                    .Select(index => (IReadOnlyList<GbcTile2Bpp>)GameBoyCameraTileTextCodec.ParseTiles(tiles.Skip((index * 4) + 40).Take(2)).ToArray())
                    .ToArray(),
                Enumerable.Range(0, 14)
                    .Select(index => (IReadOnlyList<GbcTile2Bpp>)GameBoyCameraTileTextCodec.ParseTiles(tiles.Skip((index * 4) + 42).Take(2)).ToArray())
                    .ToArray(),
                GameBoyCameraTileTextCodec.ParseTiles(tiles.Skip(96).Take(40)).ToArray());

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? FindHash(string frameId, IReadOnlyList<GbcFrameReference> frames) => frames
        .FirstOrDefault(frame => string.Equals(frame.Id, frameId, StringComparison.Ordinal))?
        .Hash;

    private sealed record FrameOverlayPayload(
        string[] Upper,
        string[][] Left,
        string[][] Right,
        string[] Lower);
}