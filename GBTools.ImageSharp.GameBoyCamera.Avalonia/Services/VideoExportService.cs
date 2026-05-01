using GBTools.ImageSharp.GameBoyCamera.Avalonia.Models;
using GBTools.ImageSharp.GameBoyCamera.Video;
using Microsoft.Extensions.Logging;

namespace GBTools.ImageSharp.GameBoyCamera.Avalonia.Services;

public sealed class VideoExportService : IVideoExportService
{
    private readonly FfmpegVideoExporter _exporter;
    private readonly ILogger<VideoExportService> _logger;

    public VideoExportService(FfmpegVideoExporter exporter, ILogger<VideoExportService> logger)
    {
        _exporter = exporter;
        _logger = logger;
    }

    public Task ExportAsync(
        IReadOnlyList<PhotoExportRequest> photos,
        VideoExportRequest request,
        IProgress<string>? consoleOutput = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(photos);
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogInformation("Exporting {Count} selected image(s) to video {Path}", photos.Count, request.OutputPath);

        return _exporter.ExportAsync(
            photos.Select(static photo => photo.Photo).ToArray(),
            new FfmpegVideoExportOptions(request.OutputPath, request.Magnification, request.FrameRate),
            consoleOutput,
            cancellationToken);
    }
}