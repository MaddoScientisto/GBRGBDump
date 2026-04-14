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
                PhotoMetadataEntryBuilder.Build(photo, album.Metadata, index)))
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
}