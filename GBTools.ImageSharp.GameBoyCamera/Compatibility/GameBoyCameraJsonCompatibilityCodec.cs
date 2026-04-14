using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GBTools.ImageSharp.GameBoyCamera.Codec;
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
            catch (Exception)
            {
                continue;
            }
        }

        if (photos.Count == 0)
        {
            throw new InvalidDataException("gb-printer-web JSON export did not contain any decodable image payloads.");
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
            Dictionary<string, object?> image = photo.RgbnData is null
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

    private static GbcTileGrid CreateGridFromJsonTiles(string[] rawTiles, JsonElement imageElement, JsonElement root, out GbcFrameOverlay? frameOverlay)
    {
        frameOverlay = null;
        int declaredTileCount = imageElement.TryGetProperty("lines", out JsonElement linesElement) && linesElement.TryGetInt32(out int parsedTileCount)
            ? parsedTileCount
            : 0;
        int tileCount = rawTiles.Length > 0 ? rawTiles.Length : declaredTileCount;
        if (tileCount <= 0)
        {
            throw new InvalidDataException("gb-printer-web JSON image payload did not contain any tile data.");
        }

        int widthInTiles = GetJsonTileGridWidth(tileCount);
        GbcTileGrid grid = rawTiles.Length > 0
            ? GameBoyCameraTileGridFactory.CreateFromTextTiles(rawTiles, widthInTiles)
            : GameBoyCameraTileGridFactory.CreateFromBinary(new byte[tileCount * GameBoyCameraConstants.TileByteCount], widthInTiles);

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
        string inflated = InflateLatin1String(compressedPayload);
        string[] rawTiles = inflated.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        GbcTileGrid grid = CreateGridFromJsonTiles(rawTiles, imageElement, root, out frameOverlay);
        return ApplyFrameMode(grid, frameMode);
    }

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
        byte[] data = Encoding.Latin1.GetBytes(payload);
        using MemoryStream input = new(data);
        using ZLibStream zlib = new(input, CompressionMode.Decompress);
        using StreamReader reader = new(zlib, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static string DeflateLatin1String(string payload)
    {
        using MemoryStream output = new();
        using (ZLibStream zlib = new(output, CompressionLevel.SmallestSize, leaveOpen: true))
        using (StreamWriter writer = new(zlib, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(payload);
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

    private static GameBoyCameraFrameMetadata? ParseMetadata(JsonElement imageElement)
    {
        if (!imageElement.TryGetProperty("meta", out JsonElement metaElement) || metaElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        int? contrast = metaElement.TryGetProperty("contrast", out JsonElement contrastElement) && contrastElement.TryGetInt32(out int parsedContrast)
            ? parsedContrast
            : null;

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
            GetOptionalString(metaElement, "romType"),
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
            GetOptionalString(metaElement, "ditherset"),
            contrast);
    }

    private static string? GetOptionalString(JsonElement objectElement, string propertyName)
    {
        return objectElement.TryGetProperty(propertyName, out JsonElement valueElement) && valueElement.ValueKind == JsonValueKind.String
            ? valueElement.GetString()
            : null;
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