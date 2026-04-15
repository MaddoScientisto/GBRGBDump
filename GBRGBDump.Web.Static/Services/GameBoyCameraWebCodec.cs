using System.IO.Compression;
using GBRGBDump.Web.Static.Models;
using GBTools.ImageSharp.GameBoyCamera;
using GBTools.ImageSharp.GameBoyCamera.Codec;
using GBTools.ImageSharp.GameBoyCamera.Compatibility;
using GBTools.ImageSharp.GameBoyCamera.Metadata;
using GBTools.ImageSharp.GameBoyCamera.Model;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace GBRGBDump.Web.Static.Services;

public sealed class GameBoyCameraWebCodec
{
    private static readonly Configuration ImageConfiguration = CreateImageConfiguration();
    private static readonly DecoderOptions DecoderOptions = new()
    {
        Configuration = ImageConfiguration,
    };

    public IReadOnlyList<string> SupportedImportExtensions { get; } =
    [
        ".sav",
        ".srm",
        ".gb",
        ".gbc",
        ".rom",
        ".bin",
        ".json",
        ".gbci",
    ];

    public IReadOnlyList<ImportedPhoto> Import(string fileName, byte[] data)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(data);

        GameBoyCameraSourceKind sourceKind = DetectSourceKind(fileName, data);
        GbcAlbum album = LoadAlbum(sourceKind, data);

        List<ImportedPhoto> photos = new(album.Photos.Count);
        for (int index = 0; index < album.Photos.Count; index++)
        {
            GbcPhoto photo = album.Photos[index];
            using Image<Rgba32> rendered = GameBoyCameraImageCodec.RenderPhoto(photo);
            using MemoryStream previewStream = new();
            rendered.Save(previewStream, new PngEncoder());

            string baseName = Path.GetFileNameWithoutExtension(fileName);
            string displayName = album.Photos.Count == 1
                ? baseName
                : $"{baseName}-{index + 1:D2}";

            photos.Add(new ImportedPhoto(
                fileName,
                index,
                displayName,
                sourceKind,
                photo,
                $"data:image/png;base64,{Convert.ToBase64String(previewStream.ToArray())}",
                rendered.Width,
                rendered.Height));
        }

        return photos;
    }

    public async Task<ExportDownload> ExportAsync(IReadOnlyList<ImportedPhoto> photos, ExportFormat format, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(photos);

        if (photos.Count == 0)
        {
            throw new InvalidOperationException("Import at least one photo before exporting.");
        }

        return format == ExportFormat.Json
            ? await ExportJsonAsync(photos, cancellationToken)
            : ExportArchive(photos, format);
    }

    private static async Task<ExportDownload> ExportJsonAsync(IReadOnlyList<ImportedPhoto> photos, CancellationToken cancellationToken)
    {
        GbcAlbum album = new(
            new GameBoyCameraAlbumMetadata(GameBoyCameraSourceKind.GbPrinterWebJson, null, 0, "album", null),
            photos.Select(static photo => photo.Photo).ToArray());

        string[] names = photos.Select(static photo => photo.DisplayName).ToArray();
        using MemoryStream stream = new();
        await GameBoyCameraCompatibility.ExportGbPrinterWebJsonAsync(
            album,
            stream,
            new GameBoyCameraJsonExportOptions
            {
                TitleFactory = (index, _) => names[index],
            },
            cancellationToken);

        return new ExportDownload("gbrgbdump-export.json", "application/json", stream.ToArray());
    }

    private static ExportDownload ExportArchive(IReadOnlyList<ImportedPhoto> photos, ExportFormat format)
    {
        using MemoryStream stream = new();
        using (ZipArchive archive = new(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            for (int index = 0; index < photos.Count; index++)
            {
                ImportedPhoto photo = photos[index];
                string entryName = $"{index + 1:D3}-{SanitizeFileName(photo.DisplayName)}.{GetFileExtension(format)}";
                ZipArchiveEntry entry = archive.CreateEntry(entryName, CompressionLevel.SmallestSize);
                using Stream entryStream = entry.Open();
                WritePhoto(entryStream, photo.Photo, format);
            }
        }

        return new ExportDownload(
            $"gbrgbdump-{format.ToString().ToLowerInvariant()}.zip",
            "application/zip",
            stream.ToArray());
    }

    private static void WritePhoto(Stream stream, GbcPhoto photo, ExportFormat format)
    {
        using Image<Rgba32> rendered = GameBoyCameraImageCodec.RenderPhoto(photo);

        switch (format)
        {
            case ExportFormat.Png:
                rendered.Save(stream, new PngEncoder());
                break;
            case ExportFormat.Gif:
                rendered.Save(stream, new GifEncoder());
                break;
            case ExportFormat.Jpeg:
                rendered.Save(stream, new JpegEncoder { Quality = 100 });
                break;
            case ExportFormat.Bmp:
                rendered.Save(stream, new BmpEncoder());
                break;
            case ExportFormat.GbBin:
                GameBoyCameraCompatibility.ExportGbBin(rendered, stream);
                break;
            case ExportFormat.Gbci:
                rendered.Save(stream, new GameBoyCameraEncoder());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported export format.");
        }
    }

    private static GbcAlbum LoadAlbum(GameBoyCameraSourceKind sourceKind, byte[] data)
    {
        using MemoryStream stream = new(data, writable: false);
        GameBoyCameraLoadOptions options = new()
        {
            SourceKind = sourceKind,
            FrameMode = GameBoyCameraFrameMode.Keep,
        };

        return sourceKind switch
        {
            GameBoyCameraSourceKind.SaveDump => GameBoyCameraCompatibility.LoadSaveAlbum(stream, options),
            GameBoyCameraSourceKind.RomDump => GameBoyCameraCompatibility.LoadRomAlbum(stream, options),
            GameBoyCameraSourceKind.GbBin => GameBoyCameraCompatibility.LoadGbBinAlbum(stream, options),
            GameBoyCameraSourceKind.GbPrinterWebJson => GameBoyCameraCompatibility.LoadGbPrinterWebAlbum(stream, options),
            GameBoyCameraSourceKind.Canonical => LoadCanonicalAlbum(stream),
            _ => throw new ArgumentOutOfRangeException(nameof(sourceKind), sourceKind, "Unsupported source kind."),
        };
    }

    private static GbcAlbum LoadCanonicalAlbum(Stream stream)
    {
        using Image<Rgba32> image = Image.Load<Rgba32>(DecoderOptions, stream);
        GbcTileGrid grid = GameBoyCameraImageCodec.EncodeToTileGrid(image);
        return new GbcAlbum(
            new GameBoyCameraAlbumMetadata(GameBoyCameraSourceKind.Canonical, null, 0, null, null),
            [new GbcPhoto(grid, null, null, null)]);
    }

    private static GameBoyCameraSourceKind DetectSourceKind(string fileName, ReadOnlySpan<byte> data)
    {
        if (data.StartsWith(GameBoyCameraConstants.CanonicalMagicHeader))
        {
            return GameBoyCameraSourceKind.Canonical;
        }

        if (data.StartsWith("GB-BIN01"u8))
        {
            return GameBoyCameraSourceKind.GbBin;
        }

        string extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".json" => GameBoyCameraSourceKind.GbPrinterWebJson,
            ".gbci" => GameBoyCameraSourceKind.Canonical,
            ".gb" or ".gbc" or ".rom" => GameBoyCameraSourceKind.RomDump,
            ".sav" or ".srm" => GameBoyCameraSourceKind.SaveDump,
            ".bin" => GameBoyCameraSourceKind.SaveDump,
            _ when LooksLikeJson(data) => GameBoyCameraSourceKind.GbPrinterWebJson,
            _ => throw new InvalidDataException($"Unsupported file type for '{fileName}'."),
        };
    }

    private static bool LooksLikeJson(ReadOnlySpan<byte> data)
    {
        for (int index = 0; index < data.Length; index++)
        {
            byte value = data[index];
            if (value is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
            {
                continue;
            }

            return value is (byte)'{' or (byte)'[';
        }

        return false;
    }

    private static string GetFileExtension(ExportFormat format) => format switch
    {
        ExportFormat.Png => "png",
        ExportFormat.Gif => "gif",
        ExportFormat.Jpeg => "jpg",
        ExportFormat.Bmp => "bmp",
        ExportFormat.GbBin => "bin",
        ExportFormat.Gbci => GameBoyCameraConstants.DefaultFileExtension,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported export format."),
    };

    private static string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "photo";
        }

        HashSet<char> invalidChars = Path.GetInvalidFileNameChars().ToHashSet();
        char[] chars = value
            .Trim()
            .Select(character => invalidChars.Contains(character) || char.IsWhiteSpace(character) ? '-' : char.ToLowerInvariant(character))
            .ToArray();

        string sanitized = new string(chars).Trim('-');
        return string.IsNullOrWhiteSpace(sanitized) ? "photo" : sanitized;
    }

    private static Configuration CreateImageConfiguration()
    {
        Configuration configuration = Configuration.Default.Clone();
        configuration.Configure(new GameBoyCameraConfigurationModule());
        return configuration;
    }
}