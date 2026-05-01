using GBTools.ImageSharp.GameBoyCamera.Avalonia.Models;
using GBTools.PicoGbPrinter.Serial;
using Microsoft.Extensions.Logging;

namespace GBTools.ImageSharp.GameBoyCamera.Avalonia.Services;

public sealed class PicoGbPrinterImportService : IPicoGbPrinterImportService
{
    private readonly IAlbumLoadService _albumLoadService;
    private readonly ILogger<PicoGbPrinterImportService> _logger;

    public PicoGbPrinterImportService(
        IAlbumLoadService albumLoadService,
        ILogger<PicoGbPrinterImportService> logger)
    {
        _albumLoadService = albumLoadService;
        _logger = logger;
    }

    public IReadOnlyList<string> GetAvailablePorts()
        => PicoGbPrinterSerialClient.GetAvailablePortNames();

    public async Task<PicoGbPrinterImportResult> ImportAsync(
        PicoGbPrinterImportRequest request,
        IProgress<PicoGbPrinterImportProgress>? progress = null,
        Func<bool>? shouldStop = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        IReadOnlyList<string> availablePorts = GetAvailablePorts();
        if (availablePorts.Count == 0)
        {
            throw new InvalidOperationException("No serial ports were detected.");
        }

        int totalSteps = string.IsNullOrWhiteSpace(request.PortName) ? 5 : 4;
        int completedSteps = 0;
        int captureCount = 0;
        List<LoadedPhotoInfo> allPhotos = [];

        string portName = await ResolvePortNameAsync(request, availablePorts, progress, totalSteps, cancellationToken).ConfigureAwait(false);
        completedSteps++;

        PicoGbPrinterClientOptions options = new()
        {
            PortName = portName,
            Trace = new Progress<string>(line => Report(progress, line, completedSteps, totalSteps, appendToLog: true)),
        };

        using PicoGbPrinterSerialClient client = new(options);

        Report(progress, $"Connecting to {portName}...", completedSteps, totalSteps);
        await client.ConnectAsync(cancellationToken).ConfigureAwait(false);
        completedSteps++;

        PicoGbPrinterStatus status = await client.ReadStatusAsync(cancellationToken).ConfigureAwait(false);
        Report(progress, $"Device status: queued={status.QueuedCaptures}, last={status.LastCaptureSize} bytes, replay={status.ReplayCaptureSize} bytes.", completedSteps, totalSteps);

        PicoGbPrinterCapture capture = request.Mode switch
        {
            PicoGbPrinterImportMode.LoadLastCapture => await LoadLastCaptureAsync(
                client,
                portName,
                progress,
                completedSteps,
                totalSteps,
                async receivedCapture =>
                {
                    captureCount++;
                    await DecodeAndReportCaptureAsync(receivedCapture, portName, captureCount, progress, completedSteps, totalSteps, allPhotos, cancellationToken).ConfigureAwait(false);
                },
                cancellationToken).ConfigureAwait(false),
            _ => await WaitForLiveCaptureAsync(
                client,
                portName,
                progress,
                shouldStop,
                completedSteps,
                totalSteps,
                async receivedCapture =>
                {
                    captureCount++;
                    await DecodeAndReportCaptureAsync(receivedCapture, portName, captureCount, progress, completedSteps, totalSteps, allPhotos, cancellationToken).ConfigureAwait(false);
                },
                cancellationToken).ConfigureAwait(false),
        };

        completedSteps++;
        LoadedAlbumResult album = new(GameBoyCameraSourceKind.PicoGbPrinterPacket, allPhotos.ToArray());

        completedSteps++;
        Report(progress, $"Decoded {album.Photos.Count} image(s) from {portName}.", completedSteps, totalSteps);
        _logger.LogInformation(
            "Imported {PhotoCount} image(s) from Pico GB Printer on {PortName}. Replay={IsReplay} Transfer={IsTransfer} CaptureCount={CaptureCount} Bytes={ByteCount}",
            album.Photos.Count,
            portName,
            capture.IsReplay,
            capture.IsTransfer,
            captureCount,
            capture.Data.Length);

        return new PicoGbPrinterImportResult(portName, album, captureCount, capture.Data.Length, capture.IsReplay, capture.IsTransfer);
    }

    private async Task<string> ResolvePortNameAsync(
        PicoGbPrinterImportRequest request,
        IReadOnlyList<string> availablePorts,
        IProgress<PicoGbPrinterImportProgress>? progress,
        int totalSteps,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.PortName))
        {
            Report(progress, $"Using selected port {request.PortName}.", 1, totalSteps);
            return request.PortName;
        }

        for (int index = 0; index < availablePorts.Count; index++)
        {
            string portName = availablePorts[index];
            Report(progress, $"Probing {portName} for a Pico GB Printer banner...", index, totalSteps);
            if (await PicoGbPrinterSerialClient.TryProbeAsync(portName, cancellationToken).ConfigureAwait(false))
            {
                Report(progress, $"Automatic selection chose {portName}.", index + 1, totalSteps);
                return portName;
            }
        }

        throw new InvalidOperationException("Automatic detection did not find a responsive Pico GB Printer.");
    }

    private static async Task<PicoGbPrinterCapture> LoadLastCaptureAsync(
        PicoGbPrinterSerialClient client,
        string portName,
        IProgress<PicoGbPrinterImportProgress>? progress,
        int completedSteps,
        int totalSteps,
        Func<PicoGbPrinterCapture, Task> onCaptureReceived,
        CancellationToken cancellationToken)
    {
        Report(progress, $"Requesting the last stored capture from {portName}...", completedSteps, totalSteps);
        PicoGbPrinterCapture? capture = await client.GetLastCaptureAsync(cancellationToken).ConfigureAwait(false);
        if (capture is null)
        {
            throw new InvalidOperationException("The Pico GB Printer does not have a stored capture yet.");
        }

        Report(progress, $"Received stored capture from {portName} ({capture.Data.Length} bytes).", completedSteps + 1, totalSteps);
        await onCaptureReceived(capture).ConfigureAwait(false);
        return capture;
    }

    private static async Task<PicoGbPrinterCapture> WaitForLiveCaptureAsync(
        PicoGbPrinterSerialClient client,
        string portName,
        IProgress<PicoGbPrinterImportProgress>? progress,
        Func<bool>? shouldStop,
        int completedSteps,
        int totalSteps,
        Func<PicoGbPrinterCapture, Task> onCaptureReceived,
        CancellationToken cancellationToken)
    {
        Report(progress, $"Waiting for live captures on {portName}. Press Stop in the serial window when you have everything you want.", completedSteps, totalSteps);
        PicoGbPrinterCapture capture = await client.CaptureUntilStoppedAsync(onCaptureReceived, shouldStop, cancellationToken: cancellationToken).ConfigureAwait(false);
        Report(progress, $"Live capture session ended on {portName} with {capture.Data.Length} bytes. Clearing any remaining replay state on the device...", completedSteps + 1, totalSteps);
        await client.ClearAsync(cancellationToken).ConfigureAwait(false);
        Report(progress, $"Device buffers cleared after the live capture session on {portName}.", completedSteps + 1, totalSteps);
        return capture;
    }

    private async Task DecodeAndReportCaptureAsync(
        PicoGbPrinterCapture capture,
        string portName,
        int captureIndex,
        IProgress<PicoGbPrinterImportProgress>? progress,
        int completedSteps,
        int totalSteps,
        List<LoadedPhotoInfo> allPhotos,
        CancellationToken cancellationToken)
    {
        string captureName = $"Pico {portName} {(capture.IsReplay ? "Replay" : "Live")} {captureIndex:D2}";
        Report(progress, $"Decoding capture {captureIndex} from {portName} ({capture.Data.Length} bytes)...", completedSteps, totalSteps);

        LoadedAlbumResult decodedAlbum = await _albumLoadService
            .LoadPicoGbPrinterCaptureAsync(capture.Data, captureName, cancellationToken)
            .ConfigureAwait(false);

        allPhotos.AddRange(decodedAlbum.Photos);
        Report(
            progress,
            $"Decoded {decodedAlbum.Photos.Count} image(s) from capture {captureIndex} on {portName}. {allPhotos.Count} image(s) total so far.",
            completedSteps,
            totalSteps,
            appendToLog: true,
            receivedPhotos: decodedAlbum.Photos);
    }

    private static void Report(
        IProgress<PicoGbPrinterImportProgress>? progress,
        string message,
        int completedSteps,
        int totalSteps,
        bool appendToLog = true,
        IReadOnlyList<LoadedPhotoInfo>? receivedPhotos = null)
    {
        progress?.Report(new PicoGbPrinterImportProgress(message, completedSteps, totalSteps, appendToLog, receivedPhotos));
    }
}