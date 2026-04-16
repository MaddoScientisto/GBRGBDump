using System.IO;
using GBTools.ImageSharp.GameBoyCamera.Avalonia.Models;
using GBTools.ImageSharp.GameBoyCamera.Codec;
using GBTools.ImageSharp.GameBoyCamera.Compatibility;
using GBTools.ImageSharp.GameBoyCamera.Metadata;
using GBTools.ImageSharp.GameBoyCamera.Model;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace GBTools.ImageSharp.GameBoyCamera.Avalonia.Services;

public sealed class ImageExportService : IImageExportService
{
    private readonly ILogger<ImageExportService> _logger;

    public ImageExportService(ILogger<ImageExportService> logger)
    {
        _logger = logger;
    }

    public async Task ExportAsync(IReadOnlyList<PhotoExportRequest> photos, ExportRequest request, string destination, bool destinationIsDirectory, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(photos);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);

        if (photos.Count == 0)
        {
            return;
        }

        if (!destinationIsDirectory && request.Format == ExportFormat.GbPrinterWebJson && photos.Count > 1)
        {
            await using FileStream combinedStream = File.Create(destination);
            await ExportJsonAsync(photos, combinedStream, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (destinationIsDirectory)
        {
            Directory.CreateDirectory(destination);

            foreach (PhotoExportRequest photo in photos)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string path = Path.Combine(destination, $"{SanitizeFileStem(photo.FileStem)}.{request.Format.GetDefaultExtension()}");
                await ExportSingleAsync(photo, request, path, cancellationToken).ConfigureAwait(false);
            }

            return;
        }

        await ExportSingleAsync(photos[0], request, destination, cancellationToken).ConfigureAwait(false);
    }

    private async Task ExportSingleAsync(PhotoExportRequest photoRequest, ExportRequest request, string path, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Exporting {Path} as {Format}", path, request.Format);

        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? Environment.CurrentDirectory);

        using Image<Rgba32> image = GameBoyCameraImageCodec.RenderPhoto(photoRequest.Photo);
        await using FileStream stream = File.Create(path);

        switch (request.Format)
        {
            case ExportFormat.Canonical:
                image.Save(stream, new GameBoyCameraEncoder());
                break;
            case ExportFormat.GbBin:
                GameBoyCameraCompatibility.ExportGbBin(image, stream);
                break;
            case ExportFormat.GbBinBase64:
                GameBoyCameraCompatibility.ExportGbBinBase64(image, stream);
                break;
            case ExportFormat.GbPrinterWebJson:
                await ExportJsonAsync(photoRequest, stream, cancellationToken).ConfigureAwait(false);
                break;
            case ExportFormat.Txt:
                GameBoyCameraCompatibility.ExportTxt(image, stream);
                break;
            case ExportFormat.Png:
                await ExportPngAsync(image, stream, request.PngMagnification, cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new InvalidOperationException($"Unsupported export format: {request.Format}.");
        }
    }

    private static async Task ExportPngAsync(Image<Rgba32> image, Stream stream, int magnification, CancellationToken cancellationToken)
    {
        if (magnification < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(magnification), "PNG magnification must be at least 1.");
        }

        if (magnification == 1)
        {
            await image.SaveAsPngAsync(stream, cancellationToken).ConfigureAwait(false);
            return;
        }

        using Image<Rgba32> scaled = image.Clone(static context => { });
        scaled.Mutate(context => context.Resize(new ResizeOptions
        {
            Mode = ResizeMode.Stretch,
            Sampler = KnownResamplers.NearestNeighbor,
            Size = new Size(image.Width * magnification, image.Height * magnification),
        }));

        await scaled.SaveAsPngAsync(stream, cancellationToken).ConfigureAwait(false);
    }

    private static Task ExportJsonAsync(PhotoExportRequest photoRequest, Stream stream, CancellationToken cancellationToken)
        => ExportJsonAsync([photoRequest], stream, cancellationToken);

    private static Task ExportJsonAsync(IReadOnlyList<PhotoExportRequest> photoRequests, Stream stream, CancellationToken cancellationToken)
    {
        GbcAlbum album = new(
            new GameBoyCameraAlbumMetadata(
                GameBoyCameraSourceKind.GbPrinterWebJson,
                photoRequests.Select(static photo => photo.Photo.Metadata?.RomType).FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)),
                photoRequests.Count,
                null,
                1),
            photoRequests.Select(static photo => photo.Photo).ToArray());

        GameBoyCameraJsonExportOptions options = new()
        {
            LastUpdateUtc = DateTimeOffset.UtcNow,
            TitleFactory = (index, _) => photoRequests[index].Title,
            CreatedFactory = (index, _) => photoRequests[index].Created,
        };

        return GameBoyCameraCompatibility.ExportGbPrinterWebJsonAsync(album, stream, options, cancellationToken);
    }

    private static string SanitizeFileStem(string fileStem)
    {
        char[] invalidChars = Path.GetInvalidFileNameChars();
        string sanitized = new(fileStem.Select(character => invalidChars.Contains(character) ? '_' : character).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "photo" : sanitized;
    }
}