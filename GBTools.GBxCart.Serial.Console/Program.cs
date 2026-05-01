using System.Diagnostics;
using System.Linq;
using GBTools.GBxCart.Serial;

return await ProgramEntry.MainAsync(args);

internal static class ProgramEntry
{
    public static async Task<int> MainAsync(string[] args)
    {
        try
        {
            if (args.Length == 0)
            {
                PrintUsage();
                return 0;
            }

            CommandArguments command = CommandArguments.Parse(args);
            return await ExecuteAsync(command).ConfigureAwait(false);
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
                return await RunWithClientAsync(command, async client =>
                {
                    GbxCartCartridgeInfo info = await client.ReadCartridgeInfoAsync().ConfigureAwait(false);
                    LogInfo($"Connection OK on {client.PortName}. Cartridge title: {info.Title}. ROM size: {info.RomSizeBytes} bytes. RAM size: {info.RamSizeBytes} bytes.");
                    return 0;
                }).ConfigureAwait(false);

            case "read-header":
                return await RunWithClientAsync(command, async client =>
                {
                    string outputPath = command.GetRequiredString("output");
                    byte[] header = await client.ReadHeaderAsync().ConfigureAwait(false);
                    EnsureParentDirectoryExists(outputPath);
                    await File.WriteAllBytesAsync(outputPath, header).ConfigureAwait(false);
                    LogInfo($"Saved {header.Length} header bytes to {outputPath}.");
                    return 0;
                }).ConfigureAwait(false);

            case "dump-save":
                return await RunWithClientAsync(command, async client =>
                {
                    string outputPath = command.GetRequiredString("output");
                    GbxCartDumpResult result = await client.DumpGameBoyCameraAsync(GbxCartDumpMode.Save).ConfigureAwait(false);
                    EnsureParentDirectoryExists(outputPath);
                    await File.WriteAllBytesAsync(outputPath, result.SaveData!).ConfigureAwait(false);
                    LogInfo($"Saved {result.SaveData!.Length} save bytes to {outputPath}.");
                    return 0;
                }).ConfigureAwait(false);

            case "dump-rom":
                return await RunWithClientAsync(command, async client =>
                {
                    string outputPath = command.GetRequiredString("output");
                    GbxCartDumpResult result = await client.DumpGameBoyCameraAsync(GbxCartDumpMode.Rom).ConfigureAwait(false);
                    EnsureParentDirectoryExists(outputPath);
                    await File.WriteAllBytesAsync(outputPath, result.RomData!).ConfigureAwait(false);
                    LogInfo($"Saved {result.RomData!.Length} ROM bytes to {outputPath}.");
                    return 0;
                }).ConfigureAwait(false);

            case "dump-both":
                return await RunWithClientAsync(command, async client =>
                {
                    string outputDirectory = command.GetRequiredString("output-dir");
                    Directory.CreateDirectory(outputDirectory);
                    GbxCartDumpResult result = await client.DumpGameBoyCameraAsync(GbxCartDumpMode.SaveAndRom).ConfigureAwait(false);
                    string baseName = string.IsNullOrWhiteSpace(result.CartridgeInfo.Title) ? "gbxcart" : result.CartridgeInfo.Title;
                    string safeBaseName = string.Concat(baseName.Select(static ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
                    string savePath = Path.Combine(outputDirectory, safeBaseName + ".sav");
                    string romPath = Path.Combine(outputDirectory, safeBaseName + ".gb");
                    await File.WriteAllBytesAsync(savePath, result.SaveData!).ConfigureAwait(false);
                    await File.WriteAllBytesAsync(romPath, result.RomData!).ConfigureAwait(false);
                    LogInfo($"Saved save dump to {savePath} and ROM dump to {romPath}.");
                    return 0;
                }).ConfigureAwait(false);

            case "smoke-test":
                return await RunWithClientAsync(command, async client =>
                {
                    GbxCartCartridgeInfo info = await client.ReadCartridgeInfoAsync().ConfigureAwait(false);
                    LogInfo($"Detected cartridge title: {info.Title}.");
                    GbxCartDumpResult result = await client.DumpGameBoyCameraAsync(GbxCartDumpMode.Save).ConfigureAwait(false);
                    if (result.SaveData is null || result.SaveData.Length != GbxCartProtocolConstants.GameBoyCameraSaveSizeBytes)
                    {
                        throw new InvalidOperationException("Smoke test save dump returned an unexpected size.");
                    }

                    LogInfo($"Smoke test passed on {client.PortName}. Save size: {result.SaveData.Length} bytes.");
                    return 0;
                }).ConfigureAwait(false);

            default:
                throw new CommandLineException($"Unknown command '{command.Name}'.");
        }
    }

    private static async Task<int> RunWithClientAsync(CommandArguments command, Func<GbxCartSerialClient, Task<int>> action)
    {
        await using var scope = new ClientScope(await OpenClientAsync(command).ConfigureAwait(false));
        return await action(scope.Client).ConfigureAwait(false);
    }

    private static async Task<GbxCartSerialClient> OpenClientAsync(CommandArguments command)
    {
        string? portName = command.GetOptionalString("port");
        if (string.IsNullOrWhiteSpace(portName))
        {
            GbxCartProbeResult probe = await GbxCartSerialClient.TryAutoDetectAsync().ConfigureAwait(false)
                ?? throw new InvalidOperationException("Automatic detection did not find a responsive GBxCart Game Boy Camera device.");
            portName = probe.PortName;
            LogInfo($"Auto-detected GBxCart on {probe.PortName} with cartridge title {probe.CartridgeInfo.Title}.");
        }

        GbxCartSerialClient client = new(new GbxCartClientOptions { PortName = portName });
        try
        {
            LogInfo($"Connecting to {portName}.");
            await client.ConnectAsync().ConfigureAwait(false);
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    private static void ListPorts()
    {
        IReadOnlyList<GbxCartPortInfo> ports = GbxCartSerialClient.GetAvailablePorts();
        if (ports.Count == 0)
        {
            LogWarning("No serial ports found.");
            return;
        }

        LogInfo($"Found {ports.Count} serial port(s).");
        for (int index = 0; index < ports.Count; index++)
        {
            GbxCartPortInfo port = ports[index];
            string usbText = port.VendorId is int vid && port.ProductId is int pid
                ? $" VID:PID={vid:X4}:{pid:X4}"
                : string.Empty;
            Console.WriteLine($"{index + 1}. {port.PortName} - {port.BestDescription}{usbText}");
        }
    }

    private static void EnsureParentDirectoryExists(string filePath)
    {
        string? directory = Path.GetDirectoryName(Path.GetFullPath(filePath));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("GBTools.GBxCart.Serial.Console commands:");
        Console.WriteLine("  list-ports");
        Console.WriteLine("  connect-test [--port COM6]");
        Console.WriteLine("  read-header --output header.bin [--port COM6]");
        Console.WriteLine("  dump-save --output camera.sav [--port COM6]");
        Console.WriteLine("  dump-rom --output camera.gb [--port COM6]");
        Console.WriteLine("  dump-both --output-dir artifacts\\gbxcart [--port COM6]");
        Console.WriteLine("  smoke-test [--port COM6]");
        Console.WriteLine();
        Console.WriteLine("Notes:");
        Console.WriteLine("  If --port is omitted, the app auto-detects a responsive GBxCart Game Boy Camera device.");
        Console.WriteLine("  list-ports prints the best available device description and VID:PID when known.");
    }

    private static void LogInfo(string message) => WriteLog("INFO", message, isError: false);

    private static void LogWarning(string message) => WriteLog("WARN", message, isError: false);

    private static void LogError(string message, Exception error) => WriteLog("ERROR", message + Environment.NewLine + FormatException(error), isError: true);

    private static void WriteLog(string level, string message, bool isError)
    {
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        foreach (string line in message.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n'))
        {
            string output = $"[{timestamp}] [{level}] {line}";
            if (isError)
            {
                Console.Error.WriteLine(output);
            }
            else
            {
                Console.WriteLine(output);
            }

            Debug.WriteLine(output);
        }
    }

    private static string FormatException(Exception error)
    {
        List<string> lines = [];
        int depth = 0;
        for (Exception? current = error; current is not null; current = current.InnerException)
        {
            string prefix = depth == 0 ? "Exception" : $"Inner {depth}";
            lines.Add($"{prefix}: {current.GetType().FullName}: {current.Message}");
            lines.Add(current.StackTrace ?? "<no stack trace>");
            depth++;
        }

        return string.Join(Environment.NewLine, lines);
    }

    private sealed class ClientScope : IAsyncDisposable
    {
        public ClientScope(GbxCartSerialClient client)
        {
            Client = client;
        }

        public GbxCartSerialClient Client { get; }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (Client.IsConnected)
                {
                    await Client.DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
                }
            }
            finally
            {
                Client.Dispose();
            }
        }
    }
}

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

        string name = args[0].Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new CommandLineException("A command is required.");
        }

        var options = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (int index = 1; index < args.Length; index++)
        {
            string token = args[index];
            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                throw new CommandLineException($"Unexpected argument '{token}'. Options must start with --.");
            }

            string optionName = token[2..];
            string? optionValue = null;
            if (index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                optionValue = args[++index];
            }

            options[optionName] = optionValue;
        }

        return new CommandArguments(name.ToLowerInvariant(), options);
    }

    public string GetRequiredString(string optionName)
    {
        string? value = GetOptionalString(optionName);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new CommandLineException($"Missing required option --{optionName}.");
        }

        return value;
    }

    public string? GetOptionalString(string optionName)
    {
        return _options.TryGetValue(optionName, out string? value) ? value : null;
    }
}

internal sealed class CommandLineException : Exception
{
    public CommandLineException(string message)
        : base(message)
    {
    }
}