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
            if (!imageElement.TryGetProperty("hash", out JsonElement hashElement) || hashElement.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            string? hash = hashElement.GetString();
            if (string.IsNullOrWhiteSpace(hash) || !root.TryGetProperty(hash, out JsonElement binaryElement) || binaryElement.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            string compressed = binaryElement.GetString()!;
            string inflated = InflateLatin1String(compressed);
            string[] rawTiles = inflated.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            GbcTileGrid grid = CreateGridFromJsonTiles(rawTiles, imageElement, root, out GbcFrameOverlay? frameOverlay);
            photos.Add(new GbcPhoto(ApplyFrameMode(grid, options.FrameMode), options.FrameMode == GameBoyCameraFrameMode.Extract ? frameOverlay : null, null, null));
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
            string rawTiles = string.Join("\n", GameBoyCameraImageCodec.FormatTiles(photo.TileGrid).Select(static tile => tile.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant()));
            string compressed = DeflateLatin1String(rawTiles);
            string hash = ComputeHash(compressed);
            payload[hash] = compressed;

            Dictionary<string, object?> image = new()
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
}