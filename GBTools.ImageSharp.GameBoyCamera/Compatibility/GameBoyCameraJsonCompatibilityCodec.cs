using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GBTools.ImageSharp.GameBoyCamera.Codec;
using GBTools.ImageSharp.GameBoyCamera.Composition;
using GBTools.ImageSharp.GameBoyCamera.Metadata;
using GBTools.ImageSharp.GameBoyCamera.Model;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace GBTools.ImageSharp.GameBoyCamera.Compatibility;

internal static class GameBoyCameraJsonCompatibilityCodec
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public static Image<Rgba32> Load(Stream stream, GameBoyCameraLoadOptions options)
        => GameBoyCameraImageCodec.RenderAlbum(LoadAlbum(stream, options), options.Palette);

    public static GbcAlbum LoadAlbum(Stream stream, GameBoyCameraLoadOptions options)
    {
        using JsonDocument document = JsonDocument.Parse(stream);
        JsonElement root = document.RootElement;
        if (!root.TryGetProperty("state", out JsonElement state))
        {
            throw new InvalidDataException("Not a gb-printer-web JSON export. Expected a root 'state' object.");
        }

        if (!state.TryGetProperty("images", out JsonElement imagesElement) || imagesElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("gb-printer-web JSON export is missing state.images.");
        }

        List<GbcPhoto> photos = [];
        List<Exception> failures = [];
        foreach (JsonElement imageElement in imagesElement.EnumerateArray())
        {
            try
            {
                GbcPhoto? photo = TryCreatePhoto(imageElement, root, options);
                if (photo is not null)
                {
                    photos.Add(photo);
                }
            }
            catch (Exception exception)
            {
                failures.Add(exception);
                continue;
            }
        }

        if (photos.Count == 0)
        {
            throw failures.Count > 0
                ? new InvalidDataException("gb-printer-web JSON export did not contain any decodable image payloads.", failures[0])
                : new InvalidDataException("gb-printer-web JSON export did not contain any decodable image payloads.");
        }

        int? version = state.TryGetProperty("version", out JsonElement versionElement) && versionElement.TryGetInt32(out int parsedVersion)
            ? parsedVersion
            : null;
        var metadata = new GameBoyCameraAlbumMetadata(GameBoyCameraSourceKind.GbPrinterWebJson, null, 0, null, version);
        return new GbcAlbum(metadata, photos);
    }

    public static async Task ExportAsync(Image image, Stream stream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(stream);

        List<GbcPhoto> photos = [];
        for (int frameIndex = 0; frameIndex < image.Frames.Count; frameIndex++)
        {
            using Image frame = image.Frames.CloneFrame(frameIndex);
            using Image<Rgba32> frameImage = frame.CloneAs<Rgba32>();
            GbcTileGrid grid = GameBoyCameraImageCodec.EncodeToTileGrid(frameImage, GameBoyCameraPalette.Default);
            photos.Add(new GbcPhoto(grid, null, null, null));
        }

        await ExportAlbumAsync(new GbcAlbum(null, photos), stream, null, cancellationToken).ConfigureAwait(false);
    }

    public static Task ExportAlbumAsync(GbcAlbum album, Stream stream, GameBoyCameraJsonExportOptions? options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(album);
        ArgumentNullException.ThrowIfNull(stream);

        options ??= new GameBoyCameraJsonExportOptions();

        List<Dictionary<string, object?>> images = [];
        Dictionary<string, object?> payload = new()
        {
            ["state"] = new Dictionary<string, object?>
            {
                ["images"] = images,
                ["lastUpdateUTC"] = options.LastUpdateUtc.ToUnixTimeSeconds(),
                ["version"] = 1,
            },
        };

        for (int photoIndex = 0; photoIndex < album.Photos.Count; photoIndex++)
        {
            GbcPhoto photo = album.Photos[photoIndex];
            Dictionary<string, object?> image = photo.AverageData is not null
                ? CreateAverageExportImage(photo, photoIndex, payload, options)
                : photo.RgbnData is null
                    ? CreateMonochromeExportImage(photo, photoIndex, payload, options)
                    : CreateRgbnExportImage(photo, photoIndex, payload, options);

            Dictionary<string, object?>? meta = CreateMetaPayload(photo.Metadata);
            if (meta is not null)
            {
                image["meta"] = meta;
            }

            images.Add(image);
        }

        return JsonSerializer.SerializeAsync(stream, payload, JsonOptions, cancellationToken);
    }

    private static GbcTileGrid CreateGridFromJsonTiles(string[] rawTiles, int declaredTileCount)
    {
        int tileCount = rawTiles.Length > 0 ? rawTiles.Length : declaredTileCount;
        if (tileCount <= 0)
        {
            throw new InvalidDataException("gb-printer-web JSON image payload did not contain any tile data.");
        }

        int widthInTiles = GetJsonTileGridWidth(tileCount);
        return rawTiles.Length > 0
            ? GameBoyCameraTileGridFactory.CreateFromTextTiles(rawTiles, widthInTiles)
            : GameBoyCameraTileGridFactory.CreateFromBinary(new byte[tileCount * GameBoyCameraConstants.TileByteCount], widthInTiles);
    }

    private static GbcTileGrid CreateGridFromJsonTiles(string[] rawTiles, JsonElement imageElement, JsonElement root, out GbcFrameOverlay? frameOverlay)
    {
        frameOverlay = null;
        int declaredTileCount = imageElement.TryGetProperty("lines", out JsonElement linesElement) && linesElement.TryGetInt32(out int parsedTileCount)
            ? parsedTileCount
            : 0;
        GbcTileGrid grid = CreateGridFromJsonTiles(rawTiles, declaredTileCount);

        if (imageElement.TryGetProperty("frame", out JsonElement frameElement) && frameElement.ValueKind == JsonValueKind.String)
        {
            string? frameHash = frameElement.GetString();
            if (!string.IsNullOrWhiteSpace(frameHash))
            {
                string frameKey = root.TryGetProperty($"frame-{frameHash}", out JsonElement prefixedFrameElement) && prefixedFrameElement.ValueKind == JsonValueKind.String
                    ? $"frame-{frameHash}"
                    : frameHash;

                if (root.TryGetProperty(frameKey, out JsonElement storedFrameElement) && storedFrameElement.ValueKind == JsonValueKind.String)
                {
                    GbcFrameOverlay overlay = GameBoyCameraFrameCodec.ParseFrameOverlay(InflateLatin1String(storedFrameElement.GetString()!));
                    frameOverlay = overlay;
                    grid = grid.WidthInTiles == GameBoyCameraConstants.FramedPhotoTileWidth
                        ? GameBoyCameraFrameCodec.ApplyFrame(GameBoyCameraFrameCodec.StripFrame(grid, 2), overlay, 2)
                        : GameBoyCameraFrameCodec.ApplyFrame(grid, overlay, 2);
                }
            }
        }

        return grid;
    }

    private static GbcPhoto? TryCreatePhoto(JsonElement imageElement, JsonElement root, GameBoyCameraLoadOptions options)
    {
        if (TryCreateAveragePhoto(imageElement, root, options, out GbcPhoto? averagePhoto))
        {
            return averagePhoto;
        }

        if (TryCreateMonochromePhoto(imageElement, root, options, out GbcPhoto? monochromePhoto))
        {
            return monochromePhoto;
        }

        if (TryCreateRgbPhoto(imageElement, root, options, out GbcPhoto? rgbPhoto))
        {
            return rgbPhoto;
        }

        return null;
    }

    private static bool TryCreateAveragePhoto(JsonElement imageElement, JsonElement root, GameBoyCameraLoadOptions options, out GbcPhoto? photo)
    {
        photo = null;
        JsonElement compositeElement = default;

        bool isAverageType = imageElement.TryGetProperty("type", out JsonElement typeElement)
            && typeElement.ValueKind == JsonValueKind.String
            && string.Equals(typeElement.GetString(), "average", StringComparison.OrdinalIgnoreCase);

        bool hasAverageComposite = imageElement.TryGetProperty("composite", out compositeElement)
            && compositeElement.ValueKind == JsonValueKind.Object
            && compositeElement.TryGetProperty("kind", out JsonElement compositeKindElement)
            && compositeKindElement.ValueKind == JsonValueKind.String
            && string.Equals(compositeKindElement.GetString(), "average", StringComparison.OrdinalIgnoreCase);

        if (!isAverageType && !hasAverageComposite)
        {
            return false;
        }

        if (TryCreateRawAveragePhoto(imageElement, root, options, compositeElement, out photo))
        {
            return true;
        }

        return TryCreateLegacyAveragePhoto(imageElement, root, compositeElement, out photo);
    }

    private static bool TryCreateRawAveragePhoto(
        JsonElement imageElement,
        JsonElement root,
        GameBoyCameraLoadOptions options,
        JsonElement compositeElement,
        out GbcPhoto? photo)
    {
        photo = null;
        if (!TryParseAverageSourceGroups(compositeElement, out IReadOnlyList<GbcAverageSourceGroup> sourceGroups))
        {
            return false;
        }

        GameBoyCameraCompositionChannelOrder channelOrder = ParseAverageChannelOrder(compositeElement);
        List<IReadOnlyList<GbcPhoto>> sourcePhotoGroups = [];

        foreach (GbcAverageSourceGroup sourceGroup in sourceGroups)
        {
            List<GbcPhoto> sourcePhotos = [];
            foreach (GbcAverageSourcePhoto sourcePhoto in sourceGroup.SourcePhotos)
            {
                if (!root.TryGetProperty(sourcePhoto.Hash, out JsonElement payloadElement) || payloadElement.ValueKind != JsonValueKind.String)
                {
                    return false;
                }

                GbcTileGrid grid = LoadGridFromStoredPayload(payloadElement.GetString()!, sourcePhoto.TileCount, options.FrameMode);
                sourcePhotos.Add(new GbcPhoto(grid, null, null, null));
            }

            sourcePhotoGroups.Add(sourcePhotos);
        }

        GameBoyCameraAverageCompositionPipeline pipeline = ParseAveragePipeline(compositeElement);
        GbcPhoto createdPhoto = pipeline == GameBoyCameraAverageCompositionPipeline.Direct
            ? GameBoyCameraCompositionService.CreateDirectAveragePhoto(sourcePhotoGroups.SelectMany(static group => group).ToArray())
            : GameBoyCameraCompositionService.CreateAveragePhotoFromSourceGroups(sourcePhotoGroups, channelOrder);
        string compositeHash = imageElement.TryGetProperty("hash", out JsonElement hashElement) && hashElement.ValueKind == JsonValueKind.String
            ? hashElement.GetString() ?? createdPhoto.AverageData!.CompositeHash
            : createdPhoto.AverageData!.CompositeHash;

        photo = createdPhoto with
        {
            AverageData = createdPhoto.AverageData! with { CompositeHash = compositeHash, ChannelOrder = channelOrder, Pipeline = pipeline },
        };
        return true;
    }

    private static bool TryCreateLegacyAveragePhoto(
        JsonElement imageElement,
        JsonElement root,
        JsonElement compositeElement,
        out GbcPhoto? photo)
    {
        photo = null;
        if (!imageElement.TryGetProperty("rendered", out JsonElement renderedElement)
            || renderedElement.ValueKind != JsonValueKind.Object
            || !renderedElement.TryGetProperty("hash", out JsonElement renderedHashElement)
            || renderedHashElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        string? renderedHash = renderedHashElement.GetString();
        if (string.IsNullOrWhiteSpace(renderedHash)
            || !root.TryGetProperty(renderedHash, out JsonElement renderedPayloadElement)
            || renderedPayloadElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        byte[] renderedPayload = InflateLatin1Bytes(renderedPayloadElement.GetString()!);
        using Image<Rgba32> rendered = Image.Load<Rgba32>(renderedPayload);

        GbcTileGrid tileGrid = TryEncodeRenderedGrid(rendered, CreateBlankGrid(rendered.Width, rendered.Height));
        GbcThumbnail thumbnail = new(GameBoyCameraImageCodec.CreateThumbnailBytes(rendered));
        GameBoyCameraFrameMetadata? metadata = ParseMetadata(imageElement);
        string compositeHash = imageElement.TryGetProperty("hash", out JsonElement hashElement) && hashElement.ValueKind == JsonValueKind.String
            ? hashElement.GetString() ?? renderedHash
            : renderedHash;

        photo = new GbcPhoto(
            tileGrid,
            null,
            metadata,
            thumbnail,
            GameBoyCameraImageCodec.CopyPixelData(rendered),
            rendered.Width,
            rendered.Height,
            null,
            CreateAverageData(compositeHash, compositeElement));
        return true;
    }

    private static bool TryCreateMonochromePhoto(JsonElement imageElement, JsonElement root, GameBoyCameraLoadOptions options, out GbcPhoto? photo)
    {
        photo = null;
        if (!imageElement.TryGetProperty("hash", out JsonElement hashElement) || hashElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        string? hash = hashElement.GetString();
        if (string.IsNullOrWhiteSpace(hash)
            || !root.TryGetProperty(hash, out JsonElement binaryElement)
            || binaryElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        GbcTileGrid grid = LoadGridFromStoredPayload(binaryElement.GetString()!, imageElement, root, options.FrameMode, out GbcFrameOverlay? frameOverlay);
        GameBoyCameraFrameMetadata? metadata = ParseMetadata(imageElement);
        photo = new GbcPhoto(grid, options.FrameMode == GameBoyCameraFrameMode.Extract ? frameOverlay : null, metadata, null);
        return true;
    }

    private static bool TryCreateRgbPhoto(JsonElement imageElement, JsonElement root, GameBoyCameraLoadOptions options, out GbcPhoto? photo)
    {
        photo = null;
        if (!imageElement.TryGetProperty("hashes", out JsonElement hashesElement) || hashesElement.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        GbcTileGrid? redGrid = TryLoadRgbChannel(root, imageElement, hashesElement, "r", options.FrameMode, out GbcFrameOverlay? redFrameOverlay);
        GbcTileGrid? greenGrid = TryLoadRgbChannel(root, imageElement, hashesElement, "g", options.FrameMode, out GbcFrameOverlay? greenFrameOverlay);
        GbcTileGrid? blueGrid = TryLoadRgbChannel(root, imageElement, hashesElement, "b", options.FrameMode, out GbcFrameOverlay? blueFrameOverlay);
        GbcTileGrid? neutralGrid = TryLoadRgbChannel(root, imageElement, hashesElement, "n", options.FrameMode, out GbcFrameOverlay? neutralFrameOverlay);

        GbcTileGrid?[] grids = [redGrid, greenGrid, blueGrid, neutralGrid];
        int tileCount = grids.Where(static grid => grid is not null).Select(static grid => grid!.Tiles.Count).DefaultIfEmpty(0).Max();
        if (tileCount <= 0)
        {
            return false;
        }

        int widthInTiles = GetJsonTileGridWidth(tileCount);
        int heightInTiles = tileCount / widthInTiles;

        JsonRgbPalette palette = JsonRgbPalette.Parse(imageElement.TryGetProperty("palette", out JsonElement paletteElement) ? paletteElement : default);
        using Image<Rgba32> rendered = RenderRgbPhoto(redGrid, greenGrid, blueGrid, neutralGrid, widthInTiles, heightInTiles, palette);

        GbcTileGrid fallbackGrid = grids.First(static grid => grid is not null)!;
        GbcTileGrid tileGrid = TryEncodeRenderedGrid(rendered, fallbackGrid);
        GbcThumbnail thumbnail = new(GameBoyCameraImageCodec.CreateThumbnailBytes(rendered));
        GbcFrameOverlay? frameOverlay = options.FrameMode == GameBoyCameraFrameMode.Extract
            ? redFrameOverlay ?? greenFrameOverlay ?? blueFrameOverlay ?? neutralFrameOverlay
            : null;
        GameBoyCameraFrameMetadata? metadata = ParseMetadata(imageElement);

        photo = new GbcPhoto(
            tileGrid,
            frameOverlay,
            metadata,
            thumbnail,
            GameBoyCameraImageCodec.CopyPixelData(rendered),
            rendered.Width,
            rendered.Height,
            CreateRgbnData(
                imageElement,
                hashesElement,
                palette,
                TryCreateChannelData(hashesElement, root, "r"),
                TryCreateChannelData(hashesElement, root, "g"),
                TryCreateChannelData(hashesElement, root, "b"),
                TryCreateChannelData(hashesElement, root, "n")));
        return true;
    }

    private static GbcRgbnData CreateRgbnData(
        JsonElement imageElement,
        JsonElement hashesElement,
        JsonRgbPalette palette,
        GbcRgbnChannelData? red,
        GbcRgbnChannelData? green,
        GbcRgbnChannelData? blue,
        GbcRgbnChannelData? neutral)
    {
        string compositeHash = imageElement.TryGetProperty("hash", out JsonElement hashElement) && hashElement.ValueKind == JsonValueKind.String
            ? hashElement.GetString() ?? ComputeRgbnCompositeHash(red, green, blue, neutral)
            : ComputeRgbnCompositeHash(red, green, blue, neutral);

        return new GbcRgbnData(
            compositeHash,
            red,
            green,
            blue,
            neutral,
            palette.Red.ToArray(),
            palette.Green.ToArray(),
            palette.Blue.ToArray(),
            palette.Neutral.ToArray(),
            palette.BlendMode);
    }

    private static GbcRgbnChannelData? TryCreateChannelData(JsonElement hashesElement, JsonElement root, string channelName)
    {
        if (!hashesElement.TryGetProperty(channelName, out JsonElement channelHashElement) || channelHashElement.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        string? hash = channelHashElement.GetString();
        if (string.IsNullOrWhiteSpace(hash)
            || !root.TryGetProperty(hash, out JsonElement payloadElement)
            || payloadElement.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return new GbcRgbnChannelData(hash, payloadElement.GetString()!);
    }

    private static GbcTileGrid? TryLoadRgbChannel(JsonElement root, JsonElement imageElement, JsonElement hashesElement, string channelName, GameBoyCameraFrameMode frameMode, out GbcFrameOverlay? frameOverlay)
    {
        frameOverlay = null;
        if (!hashesElement.TryGetProperty(channelName, out JsonElement channelHashElement) || channelHashElement.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        string? channelHash = channelHashElement.GetString();
        if (string.IsNullOrWhiteSpace(channelHash)
            || !root.TryGetProperty(channelHash, out JsonElement payloadElement)
            || payloadElement.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return LoadGridFromStoredPayload(payloadElement.GetString()!, imageElement, root, frameMode, out frameOverlay);
    }

    private static GbcTileGrid LoadGridFromStoredPayload(string compressedPayload, JsonElement imageElement, JsonElement root, GameBoyCameraFrameMode frameMode, out GbcFrameOverlay? frameOverlay)
    {
        byte[] inflated = InflateLatin1Bytes(compressedPayload);
        GbcTileGrid grid = CreateGridFromPayload(inflated, imageElement, root, out frameOverlay);
        return ApplyFrameMode(grid, frameMode);
    }

    private static GbcTileGrid LoadGridFromStoredPayload(string compressedPayload, int declaredTileCount, GameBoyCameraFrameMode frameMode)
    {
        byte[] inflated = InflateLatin1Bytes(compressedPayload);
        GbcTileGrid grid = CreateGridFromPayload(inflated, declaredTileCount);
        return ApplyFrameMode(grid, frameMode);
    }

    private static GbcTileGrid CreateGridFromPayload(byte[] inflatedPayload, JsonElement imageElement, JsonElement root, out GbcFrameOverlay? frameOverlay)
    {
        int declaredTileCount = TryGetDeclaredTileCount(imageElement);
        if (TryParseTextTilePayload(inflatedPayload, out string[] rawTiles))
        {
            return CreateGridFromJsonTiles(rawTiles, imageElement, root, out frameOverlay);
        }

        if (IsEmptyPayload(inflatedPayload) && declaredTileCount > 0)
        {
            frameOverlay = null;
            return CreateGridFromJsonTiles([], declaredTileCount);
        }

        frameOverlay = null;
        GbcTileGrid grid = CreateGridFromBinaryPayload(inflatedPayload);

        if (imageElement.TryGetProperty("frame", out JsonElement frameElement) && frameElement.ValueKind == JsonValueKind.String)
        {
            string? frameHash = frameElement.GetString();
            if (!string.IsNullOrWhiteSpace(frameHash))
            {
                string frameKey = root.TryGetProperty($"frame-{frameHash}", out JsonElement prefixedFrameElement) && prefixedFrameElement.ValueKind == JsonValueKind.String
                    ? $"frame-{frameHash}"
                    : frameHash;

                if (root.TryGetProperty(frameKey, out JsonElement storedFrameElement) && storedFrameElement.ValueKind == JsonValueKind.String)
                {
                    GbcFrameOverlay overlay = GameBoyCameraFrameCodec.ParseFrameOverlay(InflateLatin1String(storedFrameElement.GetString()!));
                    frameOverlay = overlay;
                    grid = grid.WidthInTiles == GameBoyCameraConstants.FramedPhotoTileWidth
                        ? GameBoyCameraFrameCodec.ApplyFrame(GameBoyCameraFrameCodec.StripFrame(grid, 2), overlay, 2)
                        : GameBoyCameraFrameCodec.ApplyFrame(grid, overlay, 2);
                }
            }
        }

        return grid;
    }

    private static GbcTileGrid CreateGridFromPayload(byte[] inflatedPayload, int declaredTileCount)
    {
        if (TryParseTextTilePayload(inflatedPayload, out string[] rawTiles))
        {
            return CreateGridFromJsonTiles(rawTiles, declaredTileCount);
        }

        if (IsEmptyPayload(inflatedPayload) && declaredTileCount > 0)
        {
            return CreateGridFromJsonTiles([], declaredTileCount);
        }

        return CreateGridFromBinaryPayload(inflatedPayload);
    }

    private static GbcTileGrid CreateGridFromBinaryPayload(byte[] inflatedPayload)
    {
        if (inflatedPayload.Length == 0 || inflatedPayload.Length % GameBoyCameraConstants.TileByteCount != 0)
        {
            throw new InvalidDataException("gb-printer-web JSON image payload did not contain a supported tile payload.");
        }

        int tileCount = inflatedPayload.Length / GameBoyCameraConstants.TileByteCount;
        int widthInTiles = GetJsonTileGridWidth(tileCount);
        return GameBoyCameraTileGridFactory.CreateFromBinary(inflatedPayload, widthInTiles);
    }

    private static bool TryParseTextTilePayload(byte[] inflatedPayload, out string[] rawTiles)
    {
        rawTiles = [];
        ReadOnlySpan<byte> payload = inflatedPayload;
        if (HasUtf8Bom(payload))
        {
            payload = payload[3..];
        }

        if (payload.Length == 0)
        {
            return false;
        }

        bool hasHexDigit = false;
        foreach (byte value in payload)
        {
            if ((value >= (byte)'0' && value <= (byte)'9')
                || (value >= (byte)'A' && value <= (byte)'F')
                || (value >= (byte)'a' && value <= (byte)'f'))
            {
                hasHexDigit = true;
                continue;
            }

            if (value is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
            {
                continue;
            }

            return false;
        }

        if (!hasHexDigit)
        {
            return false;
        }

        string inflated = Encoding.UTF8.GetString(payload);
        rawTiles = inflated.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return rawTiles.Length > 0;
    }

    private static bool IsEmptyPayload(byte[] inflatedPayload)
    {
        ReadOnlySpan<byte> payload = inflatedPayload;
        if (HasUtf8Bom(payload))
        {
            payload = payload[3..];
        }

        foreach (byte value in payload)
        {
            if (value is not (byte)' ' and not (byte)'\t' and not (byte)'\r' and not (byte)'\n')
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasUtf8Bom(ReadOnlySpan<byte> payload)
        => payload.Length >= 3
            && payload[0] == 0xEF
            && payload[1] == 0xBB
            && payload[2] == 0xBF;

    private static GbcTileGrid TryEncodeRenderedGrid(Image<Rgba32> rendered, GbcTileGrid fallbackGrid)
    {
        try
        {
            return GameBoyCameraImageCodec.EncodeToTileGrid(rendered, GameBoyCameraPalette.Default);
        }
        catch (ArgumentException)
        {
            return fallbackGrid;
        }
    }

    private static Image<Rgba32> RenderRgbPhoto(
        GbcTileGrid? redGrid,
        GbcTileGrid? greenGrid,
        GbcTileGrid? blueGrid,
        GbcTileGrid? neutralGrid,
        int widthInTiles,
        int heightInTiles,
        JsonRgbPalette palette)
    {
        int pixelWidth = widthInTiles * GameBoyCameraConstants.TilePixelWidth;
        int pixelHeight = heightInTiles * GameBoyCameraConstants.TilePixelHeight;
        byte[] redPixels = DecodeGridToColorIndexes(redGrid, widthInTiles, heightInTiles);
        byte[] greenPixels = DecodeGridToColorIndexes(greenGrid, widthInTiles, heightInTiles);
        byte[] bluePixels = DecodeGridToColorIndexes(blueGrid, widthInTiles, heightInTiles);
        byte[] neutralPixels = DecodeGridToColorIndexes(neutralGrid, widthInTiles, heightInTiles);

        Image<Rgba32> image = new(pixelWidth, pixelHeight);
        bool hasNeutral = neutralGrid is not null;
        for (int y = 0; y < pixelHeight; y++)
        {
            for (int x = 0; x < pixelWidth; x++)
            {
                int pixelIndex = (y * pixelWidth) + x;
                byte red = GetChannelShade(palette.Red, redPixels[pixelIndex]);
                byte green = GetChannelShade(palette.Green, greenPixels[pixelIndex]);
                byte blue = GetChannelShade(palette.Blue, bluePixels[pixelIndex]);

                if (hasNeutral)
                {
                    byte neutral = GetChannelShade(palette.Neutral, neutralPixels[pixelIndex]);

                    if (!string.Equals(NormalizeBlendMode(palette.BlendMode), "normal", StringComparison.Ordinal))
                    {
                        red = ApplyBlend(red, neutral, palette.BlendMode);
                        green = ApplyBlend(green, neutral, palette.BlendMode);
                        blue = ApplyBlend(blue, neutral, palette.BlendMode);
                    }
                }

                image[x, y] = new Rgba32(red, green, blue, 255);
            }
        }

        return image;
    }

    private static byte[] DecodeGridToColorIndexes(GbcTileGrid? grid, int widthInTiles, int heightInTiles)
    {
        int pixelWidth = widthInTiles * GameBoyCameraConstants.TilePixelWidth;
        int pixelHeight = heightInTiles * GameBoyCameraConstants.TilePixelHeight;
        byte[] pixels = new byte[pixelWidth * pixelHeight];
        if (grid is null)
        {
            return pixels;
        }

        int copiedTileWidth = Math.Min(grid.WidthInTiles, widthInTiles);
        int copiedTileHeight = Math.Min(grid.HeightInTiles, heightInTiles);
        for (int tileY = 0; tileY < copiedTileHeight; tileY++)
        {
            for (int tileX = 0; tileX < copiedTileWidth; tileX++)
            {
                byte[] tilePixels = GameBoyCameraTileCodec.DecodeToColorIndexes(grid.Tiles[(tileY * grid.WidthInTiles) + tileX]);
                for (int y = 0; y < GameBoyCameraConstants.TilePixelHeight; y++)
                {
                    for (int x = 0; x < GameBoyCameraConstants.TilePixelWidth; x++)
                    {
                        int destinationIndex = ((tileY * GameBoyCameraConstants.TilePixelHeight + y) * pixelWidth) + (tileX * GameBoyCameraConstants.TilePixelWidth) + x;
                        pixels[destinationIndex] = tilePixels[(y * GameBoyCameraConstants.TilePixelWidth) + x];
                    }
                }
            }
        }

        return pixels;
    }

    private static byte GetChannelShade(IReadOnlyList<byte> palette, byte colorIndex)
    {
        int boundedIndex = Math.Clamp((int)colorIndex, 0, 3);
        return palette[3 - boundedIndex];
    }

    private static byte ApplyBlend(byte baseValue, byte neutralValue, string blendMode)
    {
        float baseChannel = baseValue / 255f;
        float neutralChannel = neutralValue / 255f;
        float blended = NormalizeBlendMode(blendMode) switch
        {
            "addition" => MathF.Min(1f, baseChannel + neutralChannel),
            "burn" => neutralChannel <= 0f ? 0f : 1f - MathF.Min(1f, (1f - baseChannel) / neutralChannel),
            "darken" => MathF.Min(baseChannel, neutralChannel),
            "difference" => MathF.Abs(baseChannel - neutralChannel),
            "dodge" => neutralChannel >= 1f ? 1f : MathF.Min(1f, baseChannel / (1f - neutralChannel)),
            "exclusion" => baseChannel + neutralChannel - (2f * baseChannel * neutralChannel),
            "hardlight" => neutralChannel < 0.5f
                ? 2f * baseChannel * neutralChannel
                : 1f - (2f * (1f - baseChannel) * (1f - neutralChannel)),
            "lighten" => MathF.Max(baseChannel, neutralChannel),
            "multiply" => baseChannel * neutralChannel,
            "overlay" => baseChannel < 0.5f
                ? 2f * baseChannel * neutralChannel
                : 1f - (2f * (1f - baseChannel) * (1f - neutralChannel)),
            "screen" => 1f - ((1f - baseChannel) * (1f - neutralChannel)),
            "softlight" => SoftLight(baseChannel, neutralChannel),
            _ => neutralChannel,
        };

        return (byte)Math.Clamp((int)Math.Round(blended * 255f), 0, 255);
    }

    private static string NormalizeBlendMode(string blendMode)
        => string.IsNullOrWhiteSpace(blendMode)
            ? "multiply"
            : blendMode.Replace("_", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal).ToLowerInvariant();

    private static float SoftLight(float baseChannel, float neutralChannel)
    {
        return neutralChannel <= 0.5f
            ? baseChannel - ((1f - (2f * neutralChannel)) * baseChannel * (1f - baseChannel))
            : baseChannel + (((2f * neutralChannel) - 1f) * (SoftLightHighlight(baseChannel) - baseChannel));
    }

    private static float SoftLightHighlight(float baseChannel)
        => baseChannel <= 0.25f
            ? (((16f * baseChannel) - 12f) * baseChannel + 4f) * baseChannel
            : MathF.Sqrt(baseChannel);

    private static int GetJsonTileGridWidth(int tileCount)
    {
        return tileCount switch
        {
            224 => GameBoyCameraConstants.RawPhotoTileWidth,
            360 => GameBoyCameraConstants.FramedPhotoTileWidth,
            <= GameBoyCameraConstants.RawPhotoTileWidth => tileCount,
            _ when tileCount % GameBoyCameraConstants.FramedPhotoTileWidth == 0 => GameBoyCameraConstants.FramedPhotoTileWidth,
            _ when tileCount % GameBoyCameraConstants.RawPhotoTileWidth == 0 => GameBoyCameraConstants.RawPhotoTileWidth,
            _ => throw new InvalidDataException($"gb-printer-web JSON image payload had an unsupported tile count of {tileCount}."),
        };
    }

    private static GbcTileGrid ApplyFrameMode(GbcTileGrid grid, GameBoyCameraFrameMode frameMode) =>
        grid.WidthInTiles == GameBoyCameraConstants.FramedPhotoTileWidth && frameMode != GameBoyCameraFrameMode.Keep
            ? GameBoyCameraFrameCodec.StripFrame(grid, 2)
            : grid;

    private static string InflateLatin1String(string payload)
    {
        return Encoding.UTF8.GetString(InflateLatin1Bytes(payload)).TrimStart('\uFEFF');
    }

    private static string DeflateLatin1String(string payload)
    {
        return DeflateLatin1Bytes(Encoding.UTF8.GetBytes(payload));
    }

    private static byte[] InflateLatin1Bytes(string payload)
    {
        byte[] data = Encoding.Latin1.GetBytes(payload);
        using MemoryStream input = new(data);
        using ZLibStream zlib = new(input, CompressionMode.Decompress);
        using MemoryStream output = new();
        zlib.CopyTo(output);
        return output.ToArray();
    }

    private static string DeflateLatin1Bytes(byte[] payload)
    {
        using MemoryStream output = new();
        using (ZLibStream zlib = new(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            zlib.Write(payload, 0, payload.Length);
        }

        return Encoding.Latin1.GetString(output.ToArray());
    }

    private static string ComputeHash(string payload)
    {
        byte[] bytes = Encoding.Latin1.GetBytes(payload);
        byte[] hash = SHA1.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static Dictionary<string, object?> CreateMonochromeExportImage(
        GbcPhoto photo,
        int photoIndex,
        IDictionary<string, object?> payload,
        GameBoyCameraJsonExportOptions options)
    {
        string rawTiles = string.Join("\n", GameBoyCameraImageCodec.FormatTiles(photo.TileGrid).Select(static tile => tile.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant()));
        string compressed = DeflateLatin1String(rawTiles);
        string hash = ComputeHash(compressed);
        payload[hash] = compressed;

        return new Dictionary<string, object?>
        {
            ["hash"] = hash,
            ["created"] = options.CreatedFactory?.Invoke(photoIndex, photo) ?? options.LastUpdateUtc.ToString("yyyy-MM-dd HH:mm:ss:fff", CultureInfo.InvariantCulture),
            ["title"] = options.TitleFactory?.Invoke(photoIndex, photo) ?? $"Image {photoIndex + 1:D2}",
            ["lines"] = photo.TileGrid.Tiles.Count,
            ["tags"] = Array.Empty<string>(),
            ["palette"] = options.PaletteName,
            ["framePalette"] = options.FramePaletteName,
            ["invertFramePalette"] = false,
            ["invertPalette"] = false,
            ["frame"] = string.Empty,
        };
    }

    private static Dictionary<string, object?> CreateRgbnExportImage(
        GbcPhoto photo,
        int photoIndex,
        IDictionary<string, object?> payload,
        GameBoyCameraJsonExportOptions options)
    {
        GbcRgbnData rgbn = photo.RgbnData!;
        AddChannelPayload(payload, rgbn.Red);
        AddChannelPayload(payload, rgbn.Green);
        AddChannelPayload(payload, rgbn.Blue);
        AddChannelPayload(payload, rgbn.Neutral);

        Dictionary<string, object?> hashes = [];
        AddChannelHash(hashes, "r", rgbn.Red);
        AddChannelHash(hashes, "g", rgbn.Green);
        AddChannelHash(hashes, "b", rgbn.Blue);
        AddChannelHash(hashes, "n", rgbn.Neutral);

        return new Dictionary<string, object?>
        {
            ["hash"] = string.IsNullOrWhiteSpace(rgbn.CompositeHash) ? ComputeRgbnCompositeHash(rgbn.Red, rgbn.Green, rgbn.Blue, rgbn.Neutral) : rgbn.CompositeHash,
            ["created"] = options.CreatedFactory?.Invoke(photoIndex, photo) ?? options.LastUpdateUtc.ToString("yyyy-MM-dd HH:mm:ss:fff", CultureInfo.InvariantCulture),
            ["title"] = options.TitleFactory?.Invoke(photoIndex, photo) ?? $"Image {photoIndex + 1:D2}",
            ["tags"] = Array.Empty<string>(),
            ["palette"] = new Dictionary<string, object?>
            {
                ["r"] = rgbn.RedPalette.Select(static value => (int)value).ToArray(),
                ["g"] = rgbn.GreenPalette.Select(static value => (int)value).ToArray(),
                ["b"] = rgbn.BluePalette.Select(static value => (int)value).ToArray(),
                ["n"] = rgbn.NeutralPalette.Select(static value => (int)value).ToArray(),
                ["blend"] = rgbn.BlendMode,
            },
            ["hashes"] = hashes,
        };
    }

    private static Dictionary<string, object?> CreateAverageExportImage(
        GbcPhoto photo,
        int photoIndex,
        IDictionary<string, object?> payload,
        GameBoyCameraJsonExportOptions options)
    {
        GbcAverageData average = photo.AverageData!;

        foreach (GbcAverageSourceGroup sourceGroup in average.SourceGroups)
        {
            foreach (GbcAverageSourcePhoto sourcePhoto in sourceGroup.SourcePhotos)
            {
                if (!payload.ContainsKey(sourcePhoto.Hash))
                {
                    payload[sourcePhoto.Hash] = sourcePhoto.CompressedPayload;
                }
            }
        }

        return new Dictionary<string, object?>
        {
            ["hash"] = average.CompositeHash,
            ["type"] = "average",
            ["created"] = options.CreatedFactory?.Invoke(photoIndex, photo) ?? options.LastUpdateUtc.ToString("yyyy-MM-dd HH:mm:ss:fff", CultureInfo.InvariantCulture),
            ["title"] = options.TitleFactory?.Invoke(photoIndex, photo) ?? $"Image {photoIndex + 1:D2}",
            ["tags"] = Array.Empty<string>(),
            ["composite"] = new Dictionary<string, object?>
            {
                ["kind"] = "average",
                ["version"] = 2,
                ["algorithm"] = average.Algorithm,
                ["pipeline"] = average.Pipeline == GameBoyCameraAverageCompositionPipeline.Direct ? "direct" : "rgb",
                ["channelOrder"] = average.ChannelOrder.ToString().ToLowerInvariant(),
                ["groups"] = average.SourceGroups
                    .Select(static group => new Dictionary<string, object?>
                    {
                        ["sources"] = group.SourcePhotos
                            .Select(static source => new Dictionary<string, object?>
                            {
                                ["hash"] = source.Hash,
                                ["lines"] = source.TileCount,
                            })
                            .ToArray(),
                    })
                    .ToArray(),
            },
        };
    }

    private static void AddChannelPayload(IDictionary<string, object?> payload, GbcRgbnChannelData? channel)
    {
        if (channel is not null && !payload.ContainsKey(channel.Hash))
        {
            payload[channel.Hash] = channel.CompressedPayload;
        }
    }

    private static void AddChannelHash(IDictionary<string, object?> hashes, string key, GbcRgbnChannelData? channel)
    {
        if (channel is not null)
        {
            hashes[key] = channel.Hash;
        }
    }

    private static string ComputeRgbnCompositeHash(GbcRgbnChannelData? red, GbcRgbnChannelData? green, GbcRgbnChannelData? blue, GbcRgbnChannelData? neutral)
    {
        string[] parts =
        [
            $"r:{red?.Hash ?? string.Empty}",
            $"g:{green?.Hash ?? string.Empty}",
            $"b:{blue?.Hash ?? string.Empty}",
            $"n:{neutral?.Hash ?? string.Empty}",
        ];

        byte[] bytes = Encoding.UTF8.GetBytes(string.Join("|", parts));
        byte[] hash = SHA1.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static Dictionary<string, object?>? CreateMetaPayload(GameBoyCameraFrameMetadata? metadata)
    {
        if (metadata is null)
        {
            return null;
        }

        Dictionary<string, object?> meta = new();
        Add(meta, "romType", metadata.RomType);
        Add(meta, "exposure", metadata.Exposure);
        Add(meta, "captureMode", metadata.CaptureMode);
        Add(meta, "edgeExclusive", metadata.EdgeExclusive);
        Add(meta, "edgeOperation", metadata.EdgeOperation);
        Add(meta, "gain", metadata.Gain);
        Add(meta, "edgeMode", metadata.EdgeMode);
        Add(meta, "invertOut", metadata.InvertOutput);
        Add(meta, "voltageRef", metadata.VoltageReference);
        Add(meta, "zeroPoint", metadata.ZeroPoint);
        Add(meta, "vOut", metadata.VoltageOutput);
        Add(meta, "ditherset", metadata.DitherSet);

        if (metadata.Contrast is not null)
        {
            meta["contrast"] = metadata.Contrast.Value;
        }

        Add(meta, "userId", metadata.UserId);
        Add(meta, "birthDate", metadata.BirthDate);
        Add(meta, "userName", metadata.UserName);
        Add(meta, "gender", metadata.Gender);
        Add(meta, "bloodType", metadata.BloodType);
        Add(meta, "comment", metadata.Comment);

        if (metadata.IsCopy)
        {
            meta["isCopy"] = true;
        }

        return meta.Count == 0 ? null : meta;
    }

    private static void Add(IDictionary<string, object?> meta, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            meta[key] = value;
        }
    }

    private static GbcAverageData CreateAverageData(string compositeHash, JsonElement compositeElement)
    {
        if (compositeElement.ValueKind != JsonValueKind.Object)
        {
            return new GbcAverageData(
                compositeHash,
                Array.Empty<GbcAverageSourceGroup>(),
                GameBoyCameraCompositionChannelOrder.Sequential,
                "alpha-stack-v1",
                GameBoyCameraAverageCompositionPipeline.Rgb);
        }

        string algorithm = compositeElement.TryGetProperty("algorithm", out JsonElement algorithmElement) && algorithmElement.ValueKind == JsonValueKind.String
            ? algorithmElement.GetString() ?? "alpha-stack-v1"
            : "alpha-stack-v1";

        return new GbcAverageData(
            compositeHash,
            Array.Empty<GbcAverageSourceGroup>(),
            ParseAverageChannelOrder(compositeElement),
            algorithm,
            ParseAveragePipeline(compositeElement));
    }

    private static GameBoyCameraAverageCompositionPipeline ParseAveragePipeline(JsonElement compositeElement)
    {
        if (!compositeElement.TryGetProperty("pipeline", out JsonElement pipelineElement) || pipelineElement.ValueKind != JsonValueKind.String)
        {
            return GameBoyCameraAverageCompositionPipeline.Rgb;
        }

        return string.Equals(pipelineElement.GetString(), "direct", StringComparison.OrdinalIgnoreCase)
            ? GameBoyCameraAverageCompositionPipeline.Direct
            : GameBoyCameraAverageCompositionPipeline.Rgb;
    }

    private static GameBoyCameraCompositionChannelOrder ParseAverageChannelOrder(JsonElement compositeElement)
    {
        if (!compositeElement.TryGetProperty("channelOrder", out JsonElement channelOrderElement) || channelOrderElement.ValueKind != JsonValueKind.String)
        {
            return GameBoyCameraCompositionChannelOrder.Sequential;
        }

        return string.Equals(channelOrderElement.GetString(), "interleaved", StringComparison.OrdinalIgnoreCase)
            ? GameBoyCameraCompositionChannelOrder.Interleaved
            : GameBoyCameraCompositionChannelOrder.Sequential;
    }

    private static bool TryParseAverageSourceGroups(JsonElement compositeElement, out IReadOnlyList<GbcAverageSourceGroup> sourceGroups)
    {
        sourceGroups = Array.Empty<GbcAverageSourceGroup>();
        if (!compositeElement.TryGetProperty("groups", out JsonElement groupsElement) || groupsElement.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        List<GbcAverageSourceGroup> groups = [];
        foreach (JsonElement groupElement in groupsElement.EnumerateArray())
        {
            if (!groupElement.TryGetProperty("sources", out JsonElement sourcesElement) || sourcesElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            List<GbcAverageSourcePhoto> sources = [];
            foreach (JsonElement sourceElement in sourcesElement.EnumerateArray())
            {
                if (!sourceElement.TryGetProperty("hash", out JsonElement hashElement)
                    || hashElement.ValueKind != JsonValueKind.String
                    || !sourceElement.TryGetProperty("lines", out JsonElement linesElement)
                    || !linesElement.TryGetInt32(out int tileCount))
                {
                    return false;
                }

                string? hash = hashElement.GetString();
                if (string.IsNullOrWhiteSpace(hash) || tileCount <= 0)
                {
                    return false;
                }

                sources.Add(new GbcAverageSourcePhoto(hash, tileCount, string.Empty));
            }

            if (sources.Count == 0)
            {
                return false;
            }

            groups.Add(new GbcAverageSourceGroup(sources));
        }

        if (groups.Count == 0)
        {
            return false;
        }

        sourceGroups = groups;
        return true;
    }

    private static GameBoyCameraFrameMetadata? ParseMetadata(JsonElement imageElement)
    {
        if (!imageElement.TryGetProperty("meta", out JsonElement metaElement) || metaElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        int? contrast = TryParseOptionalInt(metaElement, "contrast");

        return new GameBoyCameraFrameMetadata(
            -1,
            -1,
            -1,
            -1,
            GetOptionalString(metaElement, "userId"),
            GetOptionalString(metaElement, "userName"),
            GetOptionalString(metaElement, "birthDate"),
            GetOptionalString(metaElement, "gender"),
            GetOptionalString(metaElement, "bloodType"),
            GetOptionalString(metaElement, "comment"),
            metaElement.TryGetProperty("isCopy", out JsonElement isCopyElement) && isCopyElement.ValueKind is JsonValueKind.True or JsonValueKind.False && isCopyElement.GetBoolean(),
            GetOptionalString(metaElement, "romType", "saveType"),
            GetOptionalString(metaElement, "exposure"),
            GetOptionalString(metaElement, "captureMode"),
            GetOptionalString(metaElement, "edgeExclusive"),
            GetOptionalString(metaElement, "edgeOperation"),
            GetOptionalString(metaElement, "edgeMode"),
            GetOptionalString(metaElement, "gain"),
            GetOptionalString(metaElement, "invertOut"),
            GetOptionalString(metaElement, "voltageRef"),
            GetOptionalString(metaElement, "zeroPoint"),
            GetOptionalString(metaElement, "vOut"),
            GetOptionalString(metaElement, "ditherset", "dithering"),
            contrast);
    }

    private static int TryGetDeclaredTileCount(JsonElement imageElement)
    {
        return imageElement.TryGetProperty("lines", out JsonElement linesElement) && linesElement.TryGetInt32(out int declaredTileCount)
            ? declaredTileCount
            : 0;
    }

    private static string? GetOptionalString(JsonElement objectElement, params string[] propertyNames)
    {
        foreach (string propertyName in propertyNames)
        {
            if (objectElement.TryGetProperty(propertyName, out JsonElement valueElement) && valueElement.ValueKind == JsonValueKind.String)
            {
                return valueElement.GetString();
            }
        }

        return null;
    }

    private static int? TryParseOptionalInt(JsonElement objectElement, params string[] propertyNames)
    {
        foreach (string propertyName in propertyNames)
        {
            if (!objectElement.TryGetProperty(propertyName, out JsonElement valueElement))
            {
                continue;
            }

            if (valueElement.ValueKind == JsonValueKind.Number && valueElement.TryGetInt32(out int numericValue))
            {
                return numericValue;
            }

            if (valueElement.ValueKind == JsonValueKind.String
                && int.TryParse(valueElement.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int stringValue))
            {
                return stringValue;
            }
        }

        return null;
    }

    private static byte[] CreatePngBytes(Image<Rgba32> rendered)
    {
        using MemoryStream stream = new();
        rendered.SaveAsPng(stream);
        return stream.ToArray();
    }

    private static GbcTileGrid CreateBlankGrid(int pixelWidth, int pixelHeight)
    {
        int widthInTiles = Math.Max(1, (int)Math.Ceiling(pixelWidth / (double)GameBoyCameraConstants.TilePixelWidth));
        int heightInTiles = Math.Max(1, (int)Math.Ceiling(pixelHeight / (double)GameBoyCameraConstants.TilePixelHeight));
        return GameBoyCameraTileGridFactory.CreateFromBinary(new byte[widthInTiles * heightInTiles * GameBoyCameraConstants.TileByteCount], widthInTiles);
    }

    private sealed class JsonRgbPalette
    {
        private static readonly byte[] DefaultChannel = [0, 85, 170, 255];

        public byte[] Red { get; }

        public byte[] Green { get; }

        public byte[] Blue { get; }

        public byte[] Neutral { get; }

        public string BlendMode { get; }

        private JsonRgbPalette(byte[] red, byte[] green, byte[] blue, byte[] neutral, string blendMode)
        {
            Red = red;
            Green = green;
            Blue = blue;
            Neutral = neutral;
            BlendMode = blendMode;
        }

        public static JsonRgbPalette Parse(JsonElement paletteElement)
        {
            if (paletteElement.ValueKind != JsonValueKind.Object)
            {
                return new JsonRgbPalette(DefaultChannel, DefaultChannel, DefaultChannel, DefaultChannel, "multiply");
            }

            return new JsonRgbPalette(
                ReadChannel(paletteElement, "r"),
                ReadChannel(paletteElement, "g"),
                ReadChannel(paletteElement, "b"),
                ReadChannel(paletteElement, "n"),
                paletteElement.TryGetProperty("blend", out JsonElement blendElement) && blendElement.ValueKind == JsonValueKind.String
                    ? blendElement.GetString() ?? "multiply"
                    : "multiply");
        }

        private static byte[] ReadChannel(JsonElement paletteElement, string propertyName)
        {
            if (!paletteElement.TryGetProperty(propertyName, out JsonElement channelElement) || channelElement.ValueKind != JsonValueKind.Array)
            {
                return DefaultChannel;
            }

            byte[] values = channelElement.EnumerateArray()
                .Select(static value => value.TryGetInt32(out int parsed) ? (byte)Math.Clamp(parsed, 0, 255) : (byte)0)
                .Take(4)
                .ToArray();

            return values.Length == 4 ? values : DefaultChannel;
        }
    }
}