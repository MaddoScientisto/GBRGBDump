using System.IO;
using System.Text;
using System.Globalization;
using GBTools.ImageSharp.GameBoyCamera.Avalonia.Models;
using GBTools.ImageSharp.GameBoyCamera;
using GBTools.ImageSharp.GameBoyCamera.Codec;
using GBTools.ImageSharp.GameBoyCamera.Compatibility;
using GBTools.ImageSharp.GameBoyCamera.Metadata;
using GBTools.ImageSharp.GameBoyCamera.Model;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
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

    public Task<LoadedAlbumResult> LoadPicoGbPrinterCaptureAsync(
        byte[] captureData,
        string captureName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(captureData);
        ArgumentException.ThrowIfNullOrWhiteSpace(captureName);
        return Task.Run(() => LoadPicoGbPrinterCapture(captureData, captureName), cancellationToken);
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
            GameBoyCameraSourceKind.PicoGbPrinterPacket => LoadPicoGbPrinterPacketAlbum(stream),
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

    private LoadedAlbumResult LoadPicoGbPrinterCapture(byte[] captureData, string captureName)
    {
        _logger.LogInformation("Loading in-memory Pico GB Printer capture {CaptureName} ({ByteCount} bytes).", captureName, captureData.Length);

        using MemoryStream stream = new(captureData, writable: false);
        GbcAlbum album = LoadPicoGbPrinterPacketAlbum(stream);
        string created = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss:fff", CultureInfo.InvariantCulture);

        IReadOnlyList<LoadedPhotoInfo> photos = album.Photos
            .Select((photo, index) => new LoadedPhotoInfo(
                $"{captureName} {index + 1:D2}",
                created,
                photo,
                PhotoMetadataEntryBuilder.Build(photo, album.Metadata, index, "Pico GB Printer")))
            .ToArray();

        return new LoadedAlbumResult(GameBoyCameraSourceKind.PicoGbPrinterPacket, photos);
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
        if (PicoGbPrinterPacketDecoder.LooksLikePicoStream(actualHeader))
        {
            return GameBoyCameraSourceKind.PicoGbPrinterPacket;
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

    private static GbcAlbum LoadPicoGbPrinterPacketAlbum(Stream stream)
    {
        byte[] bytes;
        using (MemoryStream buffer = new())
        {
            stream.CopyTo(buffer);
            bytes = buffer.ToArray();
        }

        IReadOnlyList<Image<Rgba32>> images = PicoGbPrinterPacketDecoder.DecodeAll(bytes);
        if (images.Count == 0)
        {
            throw new InvalidDataException("Pico GB Printer stream contained no renderable frames.");
        }

        List<GbcPhoto> photos = new(images.Count);
        foreach (Image<Rgba32> image in images)
        {
            using (image)
            {
                int width = image.Width;
                int height = image.Height;
                byte[] rgba = new byte[width * height * 4];
                image.CopyPixelDataTo(rgba);
                photos.Add(new GbcPhoto(
                    TileGrid: CreatePlaceholderTileGrid(),
                    FrameOverlay: null,
                    Metadata: null,
                    Thumbnail: null,
                    RenderedRgbaPixels: rgba,
                    RenderedWidth: width,
                    RenderedHeight: height));
            }
        }

        return new GbcAlbum(
            new GameBoyCameraAlbumMetadata(GameBoyCameraSourceKind.PicoGbPrinterPacket, null, photos.Count, null, null),
            photos);
    }

    private static GbcTileGrid CreatePlaceholderTileGrid()
    {
        // GbcPhoto requires a non-null TileGrid, but when RenderedRgbaPixels is
        // present the grid is never consulted by RenderPhoto. Provide a single
        // blank tile so the grid validates without bloating memory.
        byte[] blank = new byte[GameBoyCameraConstants.TileByteCount];
        return new GbcTileGrid(1, 1, [new GbcTile2Bpp(blank)]);
    }

    private static string CreateTitle(string path, int index) => $"{Path.GetFileName(path)} {index + 1:D2}";
}