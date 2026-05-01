using System.Globalization;
using GBTools.GBxCart.Serial;
using GBTools.ImageSharp.GameBoyCamera.Avalonia.Models;
using GBTools.ImageSharp.GameBoyCamera;
using GBTools.ImageSharp.GameBoyCamera.Compatibility;
using GBTools.ImageSharp.GameBoyCamera.Model;
using Microsoft.Extensions.Logging;

namespace GBTools.ImageSharp.GameBoyCamera.Avalonia.Services;

public sealed class GbxCartImportService : IGBxCartImportService
{
    private readonly ILogger<GbxCartImportService> _logger;

    public GbxCartImportService(ILogger<GbxCartImportService> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<GbxCartPortInfo> GetAvailablePorts() => GbxCartSerialClient.GetAvailablePorts();

    public async Task<GbxCartImportResult> ImportAsync(
        GbxCartImportRequest request,
        IProgress<GbxCartImportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        IReadOnlyList<GbxCartPortInfo> availablePorts = GetAvailablePorts();
        if (availablePorts.Count == 0)
        {
            throw new InvalidOperationException("No serial ports were detected.");
        }

        int totalSteps = CalculateTotalSteps(request, availablePorts.Count);
        int completedSteps = 0;

        string portName = await ResolvePortNameAsync(request, availablePorts, progress, totalSteps, cancellationToken).ConfigureAwait(false);
        completedSteps += request.PortName is null ? Math.Max(1, availablePorts.Count) : 1;
        Report(progress, $"Connecting to {portName}...", completedSteps, totalSteps);

        _logger.LogInformation("Detected GBxCart on {PortName}. Beginning {Mode} import.", portName, request.Mode);

        using GbxCartSerialClient client = new(new GbxCartClientOptions { PortName = portName });
        await client.ConnectAsync(cancellationToken).ConfigureAwait(false);
        completedSteps++;
        Report(progress, $"Connected to {portName}.", completedSteps, totalSteps);

        try
        {
            GbcAlbum? saveAlbum = null;
            GbcAlbum? romAlbum = null;

            if (request.Mode is GbxCartDumpMode.Save or GbxCartDumpMode.SaveAndRom)
            {
                Progress<GbxCartTransferProgress> saveProgress = new(transfer =>
                    Report(progress, transfer.Message, completedSteps + transfer.CompletedUnits, totalSteps));
                byte[] saveData = await client.ReadGameBoyCameraSaveAsync(saveProgress, cancellationToken).ConfigureAwait(false);
                completedSteps += GbxCartProtocolConstants.GameBoyCameraSaveBankCount;
                Report(progress, "Decoding save dump...", completedSteps, totalSteps);
                saveAlbum = LoadSaveAlbum(saveData, request);
                completedSteps++;
                Report(progress, $"Decoded {saveAlbum.Photos.Count} image(s) from the save dump.", completedSteps, totalSteps);
            }

            if (request.Mode is GbxCartDumpMode.Rom or GbxCartDumpMode.SaveAndRom)
            {
                Progress<GbxCartTransferProgress> romProgress = new(transfer =>
                    Report(progress, transfer.Message, completedSteps + transfer.CompletedUnits, totalSteps));
                byte[] romData = await client.ReadGameBoyCameraRomAsync(romProgress, cancellationToken).ConfigureAwait(false);
                completedSteps += GbxCartProtocolConstants.GameBoyCameraRomBankCount;
                Report(progress, "Decoding ROM dump...", completedSteps, totalSteps);
                romAlbum = LoadRomAlbum(romData, request);
                completedSteps++;
                Report(progress, $"Decoded {romAlbum.Photos.Count} image(s) from the ROM dump.", completedSteps, totalSteps);
            }

            LoadedAlbumResult album = CreateLoadedAlbum(request, saveAlbum, romAlbum);
            Report(progress, $"GBxCart import complete from {portName}.", totalSteps, totalSteps);
            return new GbxCartImportResult(portName, album);
        }
        finally
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

    private static void Report(IProgress<GbxCartImportProgress>? progress, string message, int completedSteps, int totalSteps)
    {
        progress?.Report(new GbxCartImportProgress(message, completedSteps, totalSteps));
    }

    private static int CalculateTotalSteps(GbxCartImportRequest request, int availablePortCount)
    {
        int detectionSteps = request.PortName is null ? Math.Max(1, availablePortCount) : 1;
        int transferSteps = 1;

        if (request.Mode is GbxCartDumpMode.Save or GbxCartDumpMode.SaveAndRom)
        {
            transferSteps += GbxCartProtocolConstants.GameBoyCameraSaveBankCount + 1;
        }

        if (request.Mode is GbxCartDumpMode.Rom or GbxCartDumpMode.SaveAndRom)
        {
            transferSteps += GbxCartProtocolConstants.GameBoyCameraRomBankCount + 1;
        }

        return detectionSteps + transferSteps;
    }

    private async Task<string> ResolvePortNameAsync(
        GbxCartImportRequest request,
        IReadOnlyList<GbxCartPortInfo> availablePorts,
        IProgress<GbxCartImportProgress>? progress,
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
            GbxCartPortInfo port = availablePorts[index];
            string guess = port.IsKnownUsbBridge ? "Likely GBxCart-compatible CH340 adapter" : "Unknown serial device";
            Report(progress, $"Probing {port.PortName} - {port.BestDescription} ({guess})...", index, totalSteps);

            GbxCartProbeResult? probe = await GbxCartSerialClient.TryProbeAsync(port.PortName, cancellationToken).ConfigureAwait(false);
            if (probe is not null)
            {
                Report(progress, $"Automatic selection chose {port.PortName}.", index + 1, totalSteps);
                return port.PortName;
            }
        }

        throw new InvalidOperationException("Automatic detection did not find a responsive GBxCart device.");
    }

    private static GbcAlbum LoadSaveAlbum(byte[] saveData, GbxCartImportRequest request)
    {
        using MemoryStream stream = new(saveData, writable: false);
        return GameBoyCameraCompatibility.LoadSaveAlbum(stream, CreateLoadOptions(GameBoyCameraSourceKind.SaveDump, request));
    }

    private static GbcAlbum LoadRomAlbum(byte[] romData, GbxCartImportRequest request)
    {
        using MemoryStream stream = new(romData, writable: false);
        return GameBoyCameraCompatibility.LoadRomAlbum(stream, CreateLoadOptions(GameBoyCameraSourceKind.RomDump, request));
    }

    private static GameBoyCameraLoadOptions CreateLoadOptions(GameBoyCameraSourceKind sourceKind, GbxCartImportRequest request) => new()
    {
        SourceKind = sourceKind,
        FrameMode = GameBoyCameraFrameMode.Keep,
        IncludeDeleted = !request.IgnoreDeletedPhotos,
        IncludeLastSeen = !request.IgnoreLastSeenPhoto,
        ForceMagicCheck = !request.AcceptBadDumps,
    };

    private static LoadedAlbumResult CreateLoadedAlbum(GbxCartImportRequest request, GbcAlbum? saveAlbum, GbcAlbum? romAlbum)
    {
        string created = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss:fff", CultureInfo.InvariantCulture);
        List<LoadedPhotoInfo> photos = [];

        if (saveAlbum is not null)
        {
            photos.AddRange(CreateLoadedPhotos(saveAlbum, created, "GBxCart Save", "GBxCart Save"));
        }

        if (romAlbum is not null)
        {
            photos.AddRange(CreateLoadedPhotos(romAlbum, created, "GBxCart ROM", "GBxCart ROM"));
        }

        if (photos.Count == 0)
        {
            throw new InvalidOperationException("The selected GBxCart import mode did not produce any decodable photos.");
        }

        GameBoyCameraSourceKind sourceKind = request.Mode switch
        {
            GbxCartDumpMode.Save => GameBoyCameraSourceKind.SaveDump,
            GbxCartDumpMode.Rom => GameBoyCameraSourceKind.RomDump,
            _ => GameBoyCameraSourceKind.Canonical,
        };

        return new LoadedAlbumResult(sourceKind, photos);
    }

    private static IReadOnlyList<LoadedPhotoInfo> CreateLoadedPhotos(
        GbcAlbum album,
        string created,
        string titlePrefix,
        string sourceDescription)
    {
        return album.Photos
            .Select((photo, index) => new LoadedPhotoInfo(
                $"{titlePrefix} {index + 1:D2}",
                created,
                photo,
                PhotoMetadataEntryBuilder.Build(photo, album.Metadata, index, sourceDescription)))
            .ToArray();
    }
}