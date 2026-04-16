using System.Diagnostics;
using GBTools.ImageSharp.GameBoyCamera;
using GBTools.ImageSharp.GameBoyCamera.Codec;
using GBTools.ImageSharp.GameBoyCamera.Compatibility;
using GBTools.ImageSharp.GameBoyCamera.Metadata;
using GBTools.ImageSharp.GameBoyCamera.Model;
using GBTools.PicNRec.Serial;
using SixLabors.ImageSharp;

return await ProgramEntry.MainAsync(args);

internal static class ProgramEntry
{
    private const int AutoDetectProbeAttempts = 2;
    private const int AutoDetectProbeSettleDelayMs = 150;

    public static async Task<int> MainAsync(string[] args)
    {
        try
        {
            Configuration.Default.Configure(new GameBoyCameraConfigurationModule());

            if (args.Length == 0)
            {
                PrintUsage();
                return 0;
            }

            var command = CommandArguments.Parse(args);
            return await ExecuteAsync(command);
        }
        catch (CommandLineException error)
        {
            LogError("Invalid command line arguments.", error);
            PrintUsage();
            return 2;
        }
        catch (Exception error)
        {
            LogError("Command failed.", error);
            return 1;
        }
    }

    private static async Task<int> ExecuteAsync(CommandArguments command)
    {
        switch (command.Name)
        {
            case "help":
            case "--help":
            case "-h":
                PrintUsage();
                return 0;

            case "list-ports":
                ListPorts();
                return 0;

            case "connect-test":
                return await RunWithConnectionAsync(command, static session =>
                {
                    LogInfo($"Connection OK on {session.Client.PortName} at {session.Client.CurrentBaudRate} baud.");
                    return Task.FromResult(0);
                });

            case "enter-fast-mode":
                return await RunWithConnectionAsync(command, async session =>
                {
                    if (session.Client.CurrentBaudRate == PicNRecProtocolConstants.FastBaudRate)
                    {
                        LogInfo($"Fast mode already active on {session.Client.PortName}.");
                        return 0;
                    }

                    var baudRate = await session.Client.EnterFastModeAsync();
                    LogInfo($"Fast mode active on {session.Client.PortName} at {baudRate} baud.");
                    return 0;
                }, connectInFastMode: false);

            case "read-last":
                return await RunWithConnectionAsync(command, async session =>
                {
                    var lastImageNumber = session.LastImageNumberDuringProbe ?? await session.Client.ReadLastImageNumberAsync();
                    LogInfo($"Last image number: {lastImageNumber}");
                    Console.WriteLine(lastImageNumber);
                    return 0;
                });

            case "read-metadata":
                return await RunWithConnectionAsync(command, async session =>
                {
                    var outputPath = command.GetRequiredString("output");
                    LogInfo($"Reading metadata from {session.Client.PortName}.");
                    var metadata = await session.Client.ReadMetadataAsync();
                    EnsureParentDirectoryExists(outputPath);
                    await File.WriteAllBytesAsync(outputPath, metadata);
                    LogInfo($"Saved {metadata.Length} metadata bytes to {outputPath}.");
                    return 0;
                });

            case "read-image":
                return await RunWithConnectionAsync(command, async session =>
                {
                    var imageNumber = command.GetRequiredInt("image-number");
                    var savOutput = command.GetOptionalString("output");
                    var pngOutput = command.GetOptionalString("png-output");
                    if (string.IsNullOrWhiteSpace(savOutput) && string.IsNullOrWhiteSpace(pngOutput))
                    {
                        throw new CommandLineException("read-image requires --output, --png-output, or both.");
                    }

                    LogInfo($"Reading image {imageNumber} from {session.Client.PortName}.");
                    var imageBytes = await session.Client.ReadImageAsync(imageNumber);

                    if (!string.IsNullOrWhiteSpace(savOutput))
                    {
                        EnsureParentDirectoryExists(savOutput);
                        await File.WriteAllBytesAsync(savOutput, imageBytes);
                        LogInfo($"Saved {imageBytes.Length} image bytes to {savOutput}.");
                    }

                    if (!string.IsNullOrWhiteSpace(pngOutput))
                    {
                        await SavePicNRecImageAsPngAsync(imageBytes, pngOutput);
                        LogInfo($"Rendered PNG to {pngOutput}.");
                    }

                    return 0;
                });

            case "read-images":
                return await RunWithConnectionAsync(command, async session =>
                {
                    var start = command.GetInt("start", 0);
                    var count = command.GetRequiredInt("count");
                    var outputDirectory = command.GetRequiredString("output-dir");
                    var savePng = command.HasFlag("png");
                    var saveSav = !command.HasFlag("png-only");

                    if (!savePng && !saveSav)
                    {
                        throw new CommandLineException("read-images would produce no output. Remove --png-only or add --png.");
                    }

                    Directory.CreateDirectory(outputDirectory);

                    for (var imageNumber = start; imageNumber < start + count; imageNumber++)
                    {
                        LogInfo($"Reading image {imageNumber} from {session.Client.PortName}.");
                        var imageBytes = await session.Client.ReadImageAsync(imageNumber);
                        var basePath = Path.Combine(outputDirectory, $"image_{imageNumber}");

                        if (saveSav)
                        {
                            var savPath = basePath + ".sav";
                            await File.WriteAllBytesAsync(savPath, imageBytes);
                            LogInfo($"Saved {imageBytes.Length} image bytes to {savPath}.");
                        }

                        if (savePng)
                        {
                            var pngPath = basePath + ".png";
                            await SavePicNRecImageAsPngAsync(imageBytes, pngPath);
                            LogInfo($"Rendered PNG to {pngPath}.");
                        }
                    }

                    return 0;
                });

            case "dump-all-images":
                return await RunWithConnectionAsync(command, async session =>
                {
                    var outputDirectory = command.GetRequiredString("output-dir");
                    var jsonOutput = command.GetOptionalString("json-output")
                        ?? Path.Combine(outputDirectory, "album.json");
                    var start = command.GetInt("start", 0);
                    var savePng = command.HasFlag("png");
                    var saveSav = !command.HasFlag("png-only");
                    var maxImages = command.GetInt("max-images", int.MaxValue);

                    if (!savePng && !saveSav)
                    {
                        throw new CommandLineException("dump-all-images would produce no files. Remove --png-only or add --png.");
                    }

                    var lastImageNumber = session.LastImageNumberDuringProbe ?? await session.Client.ReadLastImageNumberAsync();
                    if (lastImageNumber <= 0)
                    {
                        throw new InvalidOperationException("Device reported no available images.");
                    }

                    if (start < 0 || start >= lastImageNumber)
                    {
                        throw new CommandLineException($"--start must be between 0 and {lastImageNumber - 1}.");
                    }

                    Directory.CreateDirectory(outputDirectory);

                    var remaining = lastImageNumber - start;
                    var imageCount = Math.Min(remaining, maxImages);
                    var photos = new List<GbcPhoto>(imageCount);

                    LogInfo($"Dumping {imageCount} image(s) from {session.Client.PortName} starting at {start}. Reported available image count: {lastImageNumber}.");

                    for (var offset = 0; offset < imageCount; offset++)
                    {
                        var imageNumber = start + offset;
                        var progress = offset + 1;
                        LogInfo($"[{progress}/{imageCount}] Reading image {imageNumber} from {session.Client.PortName}.");
                        var imageBytes = await session.Client.ReadImageAsync(imageNumber);
                        photos.Add(CreatePhotoFromPicNRecImage(imageBytes, imageNumber));

                        var basePath = Path.Combine(outputDirectory, $"image_{imageNumber}");
                        if (saveSav)
                        {
                            var savPath = basePath + ".sav";
                            await File.WriteAllBytesAsync(savPath, imageBytes);
                        }

                        if (savePng)
                        {
                            var pngPath = basePath + ".png";
                            await SavePicNRecImageAsPngAsync(imageBytes, pngPath);
                        }

                        var percentage = (double)progress / imageCount * 100d;
                        LogInfo($"Progress: {progress}/{imageCount} ({percentage:F1}%).");
                    }

                    var album = new GbcAlbum(
                        new GameBoyCameraAlbumMetadata(
                            GameBoyCameraSourceKind.SaveDump,
                            null,
                            photos.Count,
                            "album",
                            null),
                        photos);

                    EnsureParentDirectoryExists(jsonOutput);
                    await using var jsonStream = File.Create(jsonOutput);
                    await GameBoyCameraCompatibility.ExportGbPrinterWebJsonAsync(
                        album,
                        jsonStream,
                        new GameBoyCameraJsonExportOptions
                        {
                            TitleFactory = (photoIndex, _) => $"image_{start + photoIndex}",
                            CreatedFactory = (photoIndex, _) => $"picnrec-{start + photoIndex:D4}",
                        });

                    LogInfo($"Combined JSON written to {jsonOutput}.");
                    return 0;
                });

            case "clear-metadata":
                if (!command.HasFlag("yes"))
                {
                    throw new CommandLineException("clear-metadata requires --yes.");
                }

                return await RunWithConnectionAsync(command, async session =>
                {
                    LogInfo($"Clearing metadata block on {session.Client.PortName}.");
                    await session.Client.ClearMetadataAsync();
                    LogInfo("Metadata block cleared.");
                    return 0;
                });

            case "render-png":
                {
                    var inputPath = command.GetRequiredString("input");
                    var outputPath = command.GetOptionalString("output")
                        ?? Path.ChangeExtension(inputPath, ".png");
                    var bytes = await File.ReadAllBytesAsync(inputPath);
                    await SavePicNRecImageAsPngAsync(bytes, outputPath);
                    LogInfo($"Rendered PNG to {outputPath}.");
                    return 0;
                }

            default:
                throw new CommandLineException($"Unknown command '{command.Name}'.");
        }
    }

    private static async Task<int> RunWithConnectionAsync(
        CommandArguments command,
        Func<ConnectionSession, Task<int>> action,
        bool? connectInFastMode = null)
    {
        ConnectionSession? session = null;
        try
        {
            session = await OpenConnectionAsync(command, connectInFastMode);
            return await action(session);
        }
        finally
        {
            if (session != null)
            {
                await DisconnectClientAsync(session.Client);
            }
        }
    }

    private static async Task<ConnectionSession> OpenConnectionAsync(CommandArguments command, bool? connectInFastModeOverride)
    {
        var connectInFastMode = connectInFastModeOverride ?? command.HasFlag("fast");
        var portName = command.GetOptionalString("port");

        if (!string.IsNullOrWhiteSpace(portName))
        {
            return await ConnectManualAsync(portName, connectInFastMode);
        }

        var ports = PicNRecSerialClient.GetAvailablePortNames();
        if (ports.Length == 0)
        {
            throw new InvalidOperationException("No serial ports found.");
        }

        var session = await TryAutoDetectClientAsync(ports, connectInFastMode);
        if (session == null)
        {
            throw new InvalidOperationException("Automatic detection did not find a responsive PicNRec device.");
        }

        LogInfo($"Connected to {session.Client.PortName} at {session.Client.CurrentBaudRate} baud.");
        return session;
    }

    private static async Task<ConnectionSession> ConnectManualAsync(string portName, bool connectInFastMode)
    {
        var client = new PicNRecSerialClient(new PicNRecClientOptions
        {
            PortName = portName,
            ConnectInFastMode = connectInFastMode,
        });

        try
        {
            LogInfo($"Connecting to {portName}.");
            await client.ConnectAsync();
            LogInfo($"Connected to {client.PortName} at {client.CurrentBaudRate} baud.");
            return new ConnectionSession(client, null);
        }
        catch (Exception error)
        {
            LogError($"Manual connect failed for {portName}.", error);
            await DisconnectClientAsync(client);
            throw;
        }
    }

    private static void ListPorts()
    {
        var ports = PicNRecSerialClient.GetAvailablePortNames();
        if (ports.Length == 0)
        {
            LogWarning("No serial ports found.");
            return;
        }

        LogInfo($"Found {ports.Length} serial port(s).");
        for (var index = 0; index < ports.Length; index++)
        {
            Console.WriteLine($"{index + 1}. {ports[index]}");
        }
    }

    private static async Task<ConnectionSession?> TryAutoDetectClientAsync(string[] ports, bool connectInFastMode)
    {
        LogInfo($"Trying automatic detection across {ports.Length} port(s).");

        for (var index = 0; index < ports.Length; index++)
        {
            var portName = ports[index];
            LogInfo($"Probing {portName} ({index + 1}/{ports.Length}).");

            for (var attempt = 1; attempt <= AutoDetectProbeAttempts; attempt++)
            {
                var candidate = new PicNRecSerialClient(new PicNRecClientOptions
                {
                    PortName = portName,
                    ConnectInFastMode = false,
                });

                try
                {
                    LogInfo($"Probe attempt {attempt}/{AutoDetectProbeAttempts} for {portName}.");
                    await candidate.ConnectAsync();
                    await Task.Delay(AutoDetectProbeSettleDelayMs).ConfigureAwait(false);
                    var lastImageNumber = await candidate.ReadLastImageNumberAsync();

                    if (connectInFastMode)
                    {
                        LogInfo($"Switching detected device on {portName} to fast mode.");
                        var baudRate = await candidate.EnterFastModeAsync();
                        LogInfo($"Fast mode active on {portName} at {baudRate} baud.");
                    }

                    LogInfo($"Detected PicNRec device on {portName}. Last image number: {lastImageNumber}.");
                    return new ConnectionSession(candidate, lastImageNumber);
                }
                catch (Exception error)
                {
                    LogError($"Probe failed for {portName} on attempt {attempt}.", error);
                    await DisconnectClientAsync(candidate);

                    if (attempt < AutoDetectProbeAttempts)
                    {
                        await Task.Delay(AutoDetectProbeSettleDelayMs).ConfigureAwait(false);
                    }
                }
            }
        }

        return null;
    }

    private static async Task DisconnectClientAsync(PicNRecSerialClient client)
    {
        try
        {
            if (client.IsConnected)
            {
                LogInfo($"Disconnecting {client.PortName}.");
                await client.DisconnectAsync();
            }
        }
        catch (Exception error)
        {
            LogError("Disconnect failed.", error);
        }
        finally
        {
            try
            {
                client.Dispose();
            }
            catch (Exception error)
            {
                LogError("Client dispose failed.", error);
            }
        }
    }

    private static async Task SavePicNRecImageAsPngAsync(byte[] imageBytes, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(imageBytes);

        if (imageBytes.Length != PicNRecProtocolConstants.ImageSize)
        {
            throw new InvalidOperationException(
                $"Expected {PicNRecProtocolConstants.ImageSize} bytes for a PicNRec image, received {imageBytes.Length}.");
        }

        EnsureParentDirectoryExists(outputPath);

        GbcTileGrid tileGrid = GameBoyCameraImageCodec.ParseBinaryTilePayload(
            imageBytes,
            GameBoyCameraConstants.RawPhotoTileWidth);
        var photo = new GbcPhoto(tileGrid, null, null, null);
        using var image = GameBoyCameraImageCodec.RenderPhoto(photo, GameBoyCameraPalette.Default);
        await image.SaveAsPngAsync(outputPath);
    }

    private static GbcPhoto CreatePhotoFromPicNRecImage(byte[] imageBytes, int imageNumber)
    {
        ArgumentNullException.ThrowIfNull(imageBytes);

        GbcTileGrid tileGrid = GameBoyCameraImageCodec.ParseBinaryTilePayload(
            imageBytes,
            GameBoyCameraConstants.RawPhotoTileWidth);

        return new GbcPhoto(
            tileGrid,
            null,
            new GameBoyCameraFrameMetadata(
                imageNumber,
                imageNumber,
                imageNumber * PicNRecProtocolConstants.ImageSize,
                0,
                null,
                null,
                null,
                null,
                null,
                null,
                false,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null),
            null);
    }

    private static void EnsureParentDirectoryExists(string filePath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(filePath));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("GBTools.PicNRec.Serial.Console commands:");
        Console.WriteLine("  list-ports");
        Console.WriteLine("  connect-test [--port COM6] [--fast]");
        Console.WriteLine("  enter-fast-mode [--port COM6]");
        Console.WriteLine("  read-last [--port COM6] [--fast]");
        Console.WriteLine("  read-metadata --output metadata.bin [--port COM6] [--fast]");
        Console.WriteLine("  read-image --image-number 42 --output image_42.sav [--png-output image_42.png] [--port COM6] [--fast]");
        Console.WriteLine("  read-images --start 0 --count 10 --output-dir artifacts\\captures [--png] [--png-only] [--port COM6] [--fast]");
        Console.WriteLine("  dump-all-images --output-dir artifacts\\dump [--json-output artifacts\\dump\\album.json] [--png] [--png-only] [--start 0] [--max-images 10] [--port COM6] [--fast]");
        Console.WriteLine("  clear-metadata --yes [--port COM6] [--fast]");
        Console.WriteLine("  render-png --input image_0.sav [--output image_0.png]");
        Console.WriteLine();
        Console.WriteLine("Notes:");
        Console.WriteLine("  If --port is omitted, the app auto-detects a responsive PicNRec device.");
        Console.WriteLine("  --fast switches the detected or selected device to 1700000 baud before the command runs.");
        Console.WriteLine("  read-images saves .sav files by default. Add --png to also render PNG files.");
    }

    private static void LogInfo(string message)
    {
        WriteLog("INFO", message, isError: false);
    }

    private static void LogWarning(string message)
    {
        WriteLog("WARN", message, isError: false);
    }

    private static void LogError(string message, Exception error)
    {
        WriteLog("ERROR", message + Environment.NewLine + FormatException(error), isError: true);
    }

    private static void WriteLog(string level, string message, bool isError)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var lines = message.Replace("\r", string.Empty).Split('\n');

        for (var index = 0; index < lines.Length; index++)
        {
            var line = $"[{timestamp}] [{level}] {lines[index]}";
            if (isError)
            {
                Console.Error.WriteLine(line);
            }
            else
            {
                Console.WriteLine(line);
            }

            Debug.WriteLine(line);
        }
    }

    private static string FormatException(Exception error)
    {
        var lines = new List<string>();
        var depth = 0;
        Exception? current = error;

        while (current != null)
        {
            var prefix = depth == 0 ? "Exception" : $"Inner {depth}";
            lines.Add($"{prefix}: {current.GetType().FullName}: {current.Message}");
            lines.Add(current.StackTrace ?? "<no stack trace>");
            current = current.InnerException;
            depth++;
        }

        return string.Join(Environment.NewLine, lines);
    }
}

internal sealed record ConnectionSession(PicNRecSerialClient Client, int? LastImageNumberDuringProbe);

internal sealed class CommandArguments
{
    private readonly Dictionary<string, string?> _options;

    private CommandArguments(string name, Dictionary<string, string?> options)
    {
        Name = name;
        _options = options;
    }

    public string Name { get; }

    public static CommandArguments Parse(string[] args)
    {
        if (args.Length == 0)
        {
            throw new CommandLineException("A command is required.");
        }

        var name = args[0].Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new CommandLineException("A command is required.");
        }

        var options = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (var index = 1; index < args.Length; index++)
        {
            var token = args[index];
            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                throw new CommandLineException($"Unexpected argument '{token}'. Options must start with --.");
            }

            var optionName = token[2..];
            if (string.IsNullOrWhiteSpace(optionName))
            {
                throw new CommandLineException("Option name cannot be empty.");
            }

            string? optionValue = null;
            if (index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                optionValue = args[index + 1];
                index++;
            }

            options[optionName] = optionValue;
        }

        return new CommandArguments(name.ToLowerInvariant(), options);
    }

    public bool HasFlag(string optionName)
    {
        if (!_options.TryGetValue(optionName, out var value))
        {
            return false;
        }

        if (value is null)
        {
            return true;
        }

        return value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || value.Equals("y", StringComparison.OrdinalIgnoreCase)
            || value.Equals("1", StringComparison.OrdinalIgnoreCase);
    }

    public string GetRequiredString(string optionName)
    {
        var value = GetOptionalString(optionName);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new CommandLineException($"Missing required option --{optionName}.");
        }

        return value;
    }

    public string? GetOptionalString(string optionName)
    {
        return _options.TryGetValue(optionName, out var value) ? value : null;
    }

    public int GetRequiredInt(string optionName)
    {
        if (!TryGetInt(optionName, out var value))
        {
            throw new CommandLineException($"Missing or invalid integer option --{optionName}.");
        }

        return value;
    }

    public int GetInt(string optionName, int defaultValue)
    {
        return TryGetInt(optionName, out var value) ? value : defaultValue;
    }

    private bool TryGetInt(string optionName, out int value)
    {
        var raw = GetOptionalString(optionName);
        return int.TryParse(raw, out value);
    }
}

internal sealed class CommandLineException : Exception
{
    public CommandLineException(string message)
        : base(message)
    {
    }
}