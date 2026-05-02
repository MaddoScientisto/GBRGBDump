using System.Globalization;
using GBTools.ImageSharp.GameBoyCamera.Avalonia.Models;
using GBTools.ImageSharp.GameBoyCamera.Codec;
using GBTools.ImageSharp.GameBoyCamera.Metadata;
using GBTools.ImageSharp.GameBoyCamera.Model;
using GBTools.PicNRec.Serial;
using Microsoft.Extensions.Logging;

namespace GBTools.ImageSharp.GameBoyCamera.Avalonia.Services;

public sealed class PicNRecImportService : IPicNRecImportService
{
    private const int AutoDetectProbeAttempts = 2;
    private const int AutoDetectProbeSettleDelayMs = 150;
    private const int MaxSupportedImageIndex = PicNRecProtocolConstants.LastImageScanBytes * 8 - 1;

    private readonly ILogger<PicNRecImportService> _logger;

    public PicNRecImportService(ILogger<PicNRecImportService> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<string> GetAvailablePorts() => PicNRecSerialClient.GetAvailablePortNames();

    public async Task<PicNRecDeviceInfo> DetectAsync(
        string? portName = null,
        IProgress<PicNRecDiscoveryProgress>? progress = null,
        IProgress<string>? serialLog = null,
        CancellationToken cancellationToken = default)
    {
        string[] ports = string.IsNullOrWhiteSpace(portName)
            ? PicNRecSerialClient.GetAvailablePortNames()
            : [portName];

        if (ports.Length == 0)
        {
            throw new InvalidOperationException("No serial ports found.");
        }

        PicNRecDetectionResult detectedDevice = await DetectDeviceAsync(ports, progress, serialLog, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Automatic detection did not find a responsive PicNRec device.");

        return new PicNRecDeviceInfo(detectedDevice.PortName, detectedDevice.LastImageNumber, MaxSupportedImageIndex);
    }

    public async Task<LoadedAlbumResult> DownloadImagesAsync(
        PicNRecDownloadRequest request,
        IProgress<PicNRecDownloadProgress>? progress = null,
        IProgress<string>? serialLog = null,
        Func<LoadedPhotoInfo, CancellationToken, Task>? onPhotoLoaded = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ImageCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "The PicNRec image range must contain at least one image.");
        }

        try
        {
            progress?.Report(new PicNRecDownloadProgress(
                request.StartImageNumber,
                0,
                request.ImageCount,
                "Starting fast-mode download.",
                ClearsDownloadedPhotos: true));
            return await DownloadFromPortAsync(request, useFastMode: true, progress, serialLog, onPhotoLoaded, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception error)
        {
            _logger.LogWarning(error, "Fast PicNRec download failed on {PortName}; retrying at the default baud rate.", request.PortName);
            progress?.Report(new PicNRecDownloadProgress(
                request.StartImageNumber,
                0,
                request.ImageCount,
                "Fast-mode download failed; retrying at the default baud rate.",
                ClearsDownloadedPhotos: true));
            return await DownloadFromPortAsync(request, useFastMode: false, progress, serialLog, onPhotoLoaded, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<LoadedPhotoInfo> PreviewImageAsync(
        string portName,
        int imageNumber,
        IProgress<string>? serialLog = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await ReadSingleImageAsync(portName, imageNumber, useFastMode: true, serialLog, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception error)
        {
            _logger.LogWarning(error, "Fast PicNRec preview failed on {PortName}; retrying at the default baud rate.", portName);
            return await ReadSingleImageAsync(portName, imageNumber, useFastMode: false, serialLog, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task ClearLastImageMarkerAsync(
        string portName,
        IProgress<string>? serialLog = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await ClearLastImageMarkerCoreAsync(portName, useFastMode: true, serialLog, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception error)
        {
            _logger.LogWarning(error, "Fast PicNRec metadata clear failed on {PortName}; retrying at the default baud rate.", portName);
            serialLog?.Report($"Fast-mode clear failed; retrying {portName} at the default baud rate.");
            await ClearLastImageMarkerCoreAsync(portName, useFastMode: false, serialLog, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<PicNRecDetectionResult?> DetectDeviceAsync(
        IReadOnlyList<string> ports,
        IProgress<PicNRecDiscoveryProgress>? progress,
        IProgress<string>? serialLog,
        CancellationToken cancellationToken)
    {
        foreach (string portName in ports)
        {
            for (int attempt = 1; attempt <= AutoDetectProbeAttempts; attempt++)
            {
                PicNRecProbeResult normalProbe = await TryProbePortAsync(portName, attempt, connectInFastMode: false, progress, serialLog, cancellationToken).ConfigureAwait(false);
                if (normalProbe.DetectionResult is not null)
                {
                    return normalProbe.DetectionResult;
                }

                if (normalProbe.OpenedPort)
                {
                    PicNRecProbeResult fastProbe = await TryProbePortAsync(portName, attempt, connectInFastMode: true, progress, serialLog, cancellationToken).ConfigureAwait(false);
                    if (fastProbe.DetectionResult is not null)
                    {
                        return fastProbe.DetectionResult;
                    }
                }

                if (attempt < AutoDetectProbeAttempts)
                {
                    await Task.Delay(AutoDetectProbeSettleDelayMs, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        return null;
    }

    private async Task<PicNRecProbeResult> TryProbePortAsync(
        string portName,
        int attempt,
        bool connectInFastMode,
        IProgress<PicNRecDiscoveryProgress>? progress,
        IProgress<string>? serialLog,
        CancellationToken cancellationToken)
    {
        string mode = connectInFastMode ? "fast-mode" : "normal-mode";
        using PicNRecSerialClient candidate = new(new PicNRecClientOptions
        {
            PortName = portName,
            ConnectInFastMode = connectInFastMode,
            Trace = serialLog is null ? null : message => serialLog.Report(message),
        });

        bool openedPort = false;

        try
        {
            progress?.Report(new PicNRecDiscoveryProgress(portName, attempt, mode, $"Trying {portName} with {mode} probe."));
            await candidate.ConnectAsync(cancellationToken).ConfigureAwait(false);
            openedPort = true;
            await Task.Delay(AutoDetectProbeSettleDelayMs, cancellationToken).ConfigureAwait(false);
            int lastImageNumber = await candidate.ReadLastImageNumberAsync(cancellationToken).ConfigureAwait(false);
            await candidate.DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
            progress?.Report(new PicNRecDiscoveryProgress(portName, attempt, mode, $"Detected PicNRec on {portName}. Last photo counter: {lastImageNumber}.", Succeeded: true));
            return new PicNRecProbeResult(new PicNRecDetectionResult(portName, lastImageNumber), openedPort);
        }
        catch (OperationCanceledException)
        {
            progress?.Report(new PicNRecDiscoveryProgress(portName, attempt, mode, $"Canceled while probing {portName}.", Succeeded: false));
            try
            {
                if (candidate.IsConnected)
                {
                    await candidate.DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch (Exception disconnectError)
            {
                _logger.LogWarning(disconnectError, "PicNRec probe disconnect failed for {PortName} after cancellation.", portName);
            }

            throw;
        }
        catch (Exception error)
        {
            _logger.LogWarning(
                error,
                "PicNRec {Mode} probe failed for {PortName} on attempt {Attempt}.",
                connectInFastMode ? "fast-mode" : "normal-mode",
                portName,
                attempt);
            progress?.Report(new PicNRecDiscoveryProgress(portName, attempt, mode, $"{portName} {mode} probe failed: {error.GetType().Name}: {error.Message}", Succeeded: false));

            try
            {
                if (candidate.IsConnected)
                {
                    await candidate.DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch (Exception disconnectError)
            {
                _logger.LogWarning(disconnectError, "PicNRec probe disconnect failed for {PortName}.", portName);
            }

            return new PicNRecProbeResult(null, openedPort);
        }
    }

    private static async Task<LoadedAlbumResult> DownloadFromPortAsync(
        PicNRecDownloadRequest request,
        bool useFastMode,
        IProgress<PicNRecDownloadProgress>? progress,
        IProgress<string>? serialLog,
        Func<LoadedPhotoInfo, CancellationToken, Task>? onPhotoLoaded,
        CancellationToken cancellationToken)
    {
        using PicNRecSerialClient client = new(new PicNRecClientOptions
        {
            PortName = request.PortName,
            Trace = serialLog is null ? null : message => serialLog.Report(message),
        });

        try
        {
            await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

            if (useFastMode)
            {
                await client.EnterFastModeAsync(cancellationToken).ConfigureAwait(false);
            }

            string created = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss:fff", CultureInfo.InvariantCulture);
            List<LoadedPhotoInfo> photos = new(request.ImageCount);

            for (int imageNumber = request.StartImageNumber; imageNumber <= request.EndImageNumber; imageNumber++)
            {
                int completed = imageNumber - request.StartImageNumber;
                progress?.Report(new PicNRecDownloadProgress(
                    imageNumber,
                    completed,
                    request.ImageCount,
                    $"Reading image {imageNumber} of {request.EndImageNumber}."));

                byte[] imageBytes = await client.ReadImageAsync(imageNumber, cancellationToken).ConfigureAwait(false);
                GbcPhoto photo = CreatePhotoFromPicNRecImage(imageBytes);
                LoadedPhotoInfo loadedPhoto = new(
                    $"PicNRec {imageNumber:D4}",
                    created,
                    photo,
                    PhotoMetadataEntryBuilder.Build(photo, null, imageNumber, "PicNRec"));
                photos.Add(loadedPhoto);

                if (onPhotoLoaded is not null)
                {
                    await onPhotoLoaded(loadedPhoto, cancellationToken).ConfigureAwait(false);
                }

                progress?.Report(new PicNRecDownloadProgress(
                    imageNumber,
                    completed + 1,
                    request.ImageCount,
                    $"Downloaded image {imageNumber} of {request.EndImageNumber}.",
                    loadedPhoto));
            }

            return new LoadedAlbumResult(GameBoyCameraSourceKind.SaveDump, photos);
        }
        finally
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

    private static async Task<LoadedPhotoInfo> ReadSingleImageAsync(
        string portName,
        int imageNumber,
        bool useFastMode,
        IProgress<string>? serialLog,
        CancellationToken cancellationToken)
    {
        using PicNRecSerialClient client = new(new PicNRecClientOptions
        {
            PortName = portName,
            Trace = serialLog is null ? null : message => serialLog.Report(message),
        });

        try
        {
            await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

            if (useFastMode)
            {
                await client.EnterFastModeAsync(cancellationToken).ConfigureAwait(false);
            }

            byte[] imageBytes = await client.ReadImageAsync(imageNumber, cancellationToken).ConfigureAwait(false);
            GbcPhoto photo = CreatePhotoFromPicNRecImage(imageBytes);
            string created = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss:fff", CultureInfo.InvariantCulture);
            return new LoadedPhotoInfo(
                $"PicNRec {imageNumber:D4}",
                created,
                photo,
                PhotoMetadataEntryBuilder.Build(photo, null, imageNumber, "PicNRec"));
        }
        finally
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

    private static async Task ClearLastImageMarkerCoreAsync(
        string portName,
        bool useFastMode,
        IProgress<string>? serialLog,
        CancellationToken cancellationToken)
    {
        using PicNRecSerialClient client = new(new PicNRecClientOptions
        {
            PortName = portName,
            Trace = serialLog is null ? null : message => serialLog.Report(message),
        });

        try
        {
            await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

            if (useFastMode)
            {
                await client.EnterFastModeAsync(cancellationToken).ConfigureAwait(false);
            }

            await client.ClearMetadataAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

    private static GbcPhoto CreatePhotoFromPicNRecImage(byte[] imageBytes)
    {
        GbcTileGrid tileGrid = GameBoyCameraImageCodec.ParseBinaryTilePayload(
            imageBytes,
            GameBoyCameraConstants.RawPhotoTileWidth);

        return new GbcPhoto(tileGrid, null, null, null);
    }

    private sealed record PicNRecDetectionResult(string PortName, int LastImageNumber);

    private sealed record PicNRecProbeResult(PicNRecDetectionResult? DetectionResult, bool OpenedPort);
}