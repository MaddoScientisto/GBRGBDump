using System.IO;
using System.Text;
using System.Globalization;
using GBTools.ImageSharp.GameBoyCamera.Avalonia.Models;
using GBTools.ImageSharp.GameBoyCamera.Codec;
using GBTools.ImageSharp.GameBoyCamera.Compatibility;
using GBTools.ImageSharp.GameBoyCamera.Metadata;
using GBTools.ImageSharp.GameBoyCamera.Model;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;

namespace GBTools.ImageSharp.GameBoyCamera.Avalonia.Services;

public sealed class AlbumLoadService : IAlbumLoadService
{
    private readonly ILogger<AlbumLoadService> _logger;

    public AlbumLoadService(ILogger<AlbumLoadService> logger)
    {
        _logger = logger;
    }

    public Task<LoadedAlbumResult> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Task.Run(() => Load(path), cancellationToken);
    }

    private LoadedAlbumResult Load(string path)
    {
        using FileStream stream = File.OpenRead(path);
        GameBoyCameraSourceKind detectedSourceKind = DetectSourceKind(stream, path);
        stream.Position = 0;

        _logger.LogInformation("Loading {Path} as {SourceKind}", path, detectedSourceKind);

        GameBoyCameraLoadOptions loadOptions = new()
        {
            SourceKind = detectedSourceKind,
            FrameMode = GameBoyCameraFrameMode.Keep,
        };

        GbcAlbum album = detectedSourceKind switch
        {
            GameBoyCameraSourceKind.Canonical => LoadCanonicalAlbum(stream),
            GameBoyCameraSourceKind.SaveDump => GameBoyCameraCompatibility.LoadSaveAlbum(stream, loadOptions),
            GameBoyCameraSourceKind.RomDump => GameBoyCameraCompatibility.LoadRomAlbum(stream, loadOptions),
            GameBoyCameraSourceKind.GbBin => GameBoyCameraCompatibility.LoadGbBinAlbum(stream, loadOptions),
            GameBoyCameraSourceKind.GbPrinterWebJson => GameBoyCameraCompatibility.LoadGbPrinterWebAlbum(stream, loadOptions),
            _ => throw new InvalidDataException($"Unsupported source kind: {detectedSourceKind}."),
        };

        IReadOnlyList<LoadedPhotoInfo> photos = album.Photos
            .Select((photo, index) => new LoadedPhotoInfo(
                CreateTitle(path, index),
                File.GetLastWriteTimeUtc(path).ToString("yyyy-MM-dd HH:mm:ss:fff", CultureInfo.InvariantCulture),
                photo,
                BuildMetadataEntries(photo, album.Metadata, index)))
            .ToArray();

        return new LoadedAlbumResult(detectedSourceKind, photos);
    }

    private static GbcAlbum LoadCanonicalAlbum(Stream stream)
    {
        Configuration configuration = Configuration.Default.Clone();
        new GameBoyCameraConfigurationModule().Configure(configuration);
        DecoderOptions decoderOptions = new()
        {
            Configuration = configuration,
        };

        using Image<Rgba32> image = Image.Load<Rgba32>(decoderOptions, stream);
        GbcTileGrid grid = GameBoyCameraImageCodec.EncodeToTileGrid(image, GameBoyCameraPalette.Default);
        GbcPhoto photo = new(grid, null, null, null);
        return new GbcAlbum(new GameBoyCameraAlbumMetadata(GameBoyCameraSourceKind.Canonical, null, 1, null, null), [photo]);
    }

    private static GameBoyCameraSourceKind DetectSourceKind(Stream stream, string path)
    {
        Span<byte> header = stackalloc byte[16];
        int bytesRead = stream.Read(header);
        ReadOnlySpan<byte> actualHeader = header[..bytesRead];

        if (actualHeader.StartsWith(GameBoyCameraConstants.CanonicalMagicHeader))
        {
            return GameBoyCameraSourceKind.Canonical;
        }

        if (actualHeader.StartsWith("GB-BIN01"u8))
        {
            return GameBoyCameraSourceKind.GbBin;
        }

        string extension = Path.GetExtension(path).ToLowerInvariant();
        return extension switch
        {
            ".gbci" => GameBoyCameraSourceKind.Canonical,
            ".sav" => GameBoyCameraSourceKind.SaveDump,
            ".gb" or ".gbc" => GameBoyCameraSourceKind.RomDump,
            ".json" => GameBoyCameraSourceKind.GbPrinterWebJson,
            ".bin" => GameBoyCameraSourceKind.GbBin,
            _ when LooksLikeJson(actualHeader) => GameBoyCameraSourceKind.GbPrinterWebJson,
            _ => throw new InvalidDataException("The selected file is not a supported Game Boy Camera image source."),
        };
    }

    private static bool LooksLikeJson(ReadOnlySpan<byte> header)
    {
        string trimmed = Encoding.UTF8.GetString(header).TrimStart();
        return trimmed.StartsWith("{", StringComparison.Ordinal);
    }

    private static string CreateTitle(string path, int index) => $"{Path.GetFileName(path)} {index + 1:D2}";

    private static IReadOnlyList<MetadataEntry> BuildMetadataEntries(GbcPhoto photo, GameBoyCameraAlbumMetadata? albumMetadata, int index)
    {
        List<MetadataEntry> entries =
        [
            new("Index", (index + 1).ToString()),
            new("Dimensions", $"{photo.TileGrid.WidthInTiles * 8}x{photo.TileGrid.HeightInTiles * 8}"),
            new("Source Kind", albumMetadata?.SourceKind.ToString() ?? "Unknown"),
        ];

        Add(entries, "Source ROM", albumMetadata?.SourceRomType);
        Add(entries, "Import Ordering", albumMetadata?.ImportOrdering);
        Add(entries, "JSON Version", albumMetadata?.JsonCompatibilityVersion?.ToString());

        GameBoyCameraFrameMetadata? metadata = photo.Metadata;
        if (metadata is null)
        {
            return entries;
        }

        Add(entries, "Album Index", metadata.AlbumIndex >= 0 ? metadata.AlbumIndex.ToString() : null);
        Add(entries, "Cart Index", metadata.CartIndex >= 0 ? metadata.CartIndex.ToString() : null);
        Add(entries, "Base Address", metadata.BaseAddress > 0 ? $"0x{metadata.BaseAddress:X}" : null);
        Add(entries, "Frame Number", metadata.FrameNumber.ToString());
        Add(entries, "User ID", metadata.UserId);
        Add(entries, "User Name", metadata.UserName);
        Add(entries, "Birth Date", metadata.BirthDate);
        Add(entries, "Gender", metadata.Gender);
        Add(entries, "Blood Type", metadata.BloodType);
        Add(entries, "Comment", metadata.Comment);
        Add(entries, "Is Copy", metadata.IsCopy ? "Yes" : "No");
        Add(entries, "ROM Type", metadata.RomType);
        Add(entries, "Exposure", metadata.Exposure);
        Add(entries, "Capture Mode", metadata.CaptureMode);
        Add(entries, "Edge Exclusive", metadata.EdgeExclusive);
        Add(entries, "Edge Operation", metadata.EdgeOperation);
        Add(entries, "Edge Mode", metadata.EdgeMode);
        Add(entries, "Gain", metadata.Gain);
        Add(entries, "Invert Output", metadata.InvertOutput);
        Add(entries, "Voltage Reference", metadata.VoltageReference);
        Add(entries, "Zero Point", metadata.ZeroPoint);
        Add(entries, "Voltage Output", metadata.VoltageOutput);
        Add(entries, "Dither Set", metadata.DitherSet);
        Add(entries, "Contrast", metadata.Contrast?.ToString());

        return entries;
    }

    private static void Add(ICollection<MetadataEntry> entries, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            entries.Add(new MetadataEntry(label, value));
        }
    }
}