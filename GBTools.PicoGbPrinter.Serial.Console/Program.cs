using System.Diagnostics;
using GBTools.PicoGbPrinter.Serial;

return await ProgramEntry.MainAsync(args);

internal static class ProgramEntry
{
    public static async Task<int> MainAsync(string[] args)
    {
        try
        {
            var command = CommandArguments.Parse(args);
            if (command.CommandName.Length == 0 || command.HasFlag("help"))
            {
                PrintUsage();
                return 0;
            }

            return await ExecuteAsync(command).ConfigureAwait(false);
        }
        catch (CommandLineException error)
        {
            LogError(error.Message);
            PrintUsage();
            return 2;
        }
        catch (Exception error)
        {
            LogError(error.ToString());
            return 1;
        }
    }

    private static async Task<int> ExecuteAsync(CommandArguments command)
    {
        switch (command.CommandName)
        {
            case "list-ports":
                ListPorts();
                return 0;
            case "probe":
                await ProbeAsync(command).ConfigureAwait(false);
                return 0;
            case "status":
                await RunWithClientAsync(command, async client => PrintStatus(await client.ReadStatusAsync().ConfigureAwait(false))).ConfigureAwait(false);
                return 0;
            case "wait-next":
                await RunWithClientAsync(command, async client => await SaveCaptureAsync(command, await client.WaitForNextCaptureAsync().ConfigureAwait(false)).ConfigureAwait(false)).ConfigureAwait(false);
                return 0;
            case "get-next":
                await RunWithClientAsync(command, async client => await SaveOptionalCaptureAsync(command, await client.GetNextCaptureAsync().ConfigureAwait(false)).ConfigureAwait(false)).ConfigureAwait(false);
                return 0;
            case "get-last":
                await RunWithClientAsync(command, async client => await SaveOptionalCaptureAsync(command, await client.GetLastCaptureAsync().ConfigureAwait(false)).ConfigureAwait(false)).ConfigureAwait(false);
                return 0;
            case "clear":
                await RunWithClientAsync(command, async client => await client.ClearAsync().ConfigureAwait(false)).ConfigureAwait(false);
                LogInfo("Cleared queued and replay captures.");
                return 0;
            case "debug":
                await RunWithClientAsync(command, async client => await client.SetDebugAsync(command.GetRequiredBool("enabled")).ConfigureAwait(false)).ConfigureAwait(false);
                return 0;
            case "click":
                await RunWithClientAsync(command, async client => await client.ClickAsync((byte)command.GetRequiredInt("mask")).ConfigureAwait(false)).ConfigureAwait(false);
                return 0;
            default:
                throw new CommandLineException($"Unknown command '{command.CommandName}'.");
        }
    }

    private static async Task ProbeAsync(CommandArguments command)
    {
        var explicitPort = command.GetOptionalString("port");
        if (!string.IsNullOrWhiteSpace(explicitPort))
        {
            var ok = await PicoGbPrinterSerialClient.TryProbeAsync(explicitPort).ConfigureAwait(false);
            LogInfo(ok ? $"Pico GB Printer responded on {explicitPort}." : $"No Pico GB Printer banner on {explicitPort}.");
            return;
        }

        var probe = await PicoGbPrinterSerialClient.TryAutoDetectAsync().ConfigureAwait(false);
        if (probe == null)
        {
            LogWarning("No Pico GB Printer serial device detected.");
            return;
        }

        LogInfo($"Pico GB Printer detected on {probe.PortName}.");
    }

    private static async Task RunWithClientAsync(CommandArguments command, Func<PicoGbPrinterSerialClient, Task> action)
    {
        var portName = command.GetOptionalString("port");
        if (string.IsNullOrWhiteSpace(portName))
        {
            var probe = await PicoGbPrinterSerialClient.TryAutoDetectAsync().ConfigureAwait(false);
            if (probe == null) throw new CommandLineException("No --port supplied and auto-detect did not find a Pico GB Printer.");
            portName = probe.PortName;
            LogInfo($"Using auto-detected port {portName}.");
        }

        using var client = new PicoGbPrinterSerialClient(new PicoGbPrinterClientOptions { PortName = portName });
        await client.ConnectAsync().ConfigureAwait(false);
        await action(client).ConfigureAwait(false);
    }

    private static async Task SaveOptionalCaptureAsync(CommandArguments command, PicoGbPrinterCapture? capture)
    {
        if (capture == null)
        {
            LogWarning("No capture available.");
            return;
        }

        await SaveCaptureAsync(command, capture).ConfigureAwait(false);
    }

    private static async Task SaveCaptureAsync(CommandArguments command, PicoGbPrinterCapture capture)
    {
        var output = command.GetOptionalString("output") ?? DefaultOutputPath(capture);
        await File.WriteAllBytesAsync(output, capture.Data).ConfigureAwait(false);
        LogInfo($"Saved {capture.Data.Length} bytes to {output}.");
        if (capture.IsReplay) LogInfo("Capture was replayed from device memory.");
        if (capture.IsTransfer) LogInfo("Capture was marked as a transfer capture.");
    }

    private static string DefaultOutputPath(PicoGbPrinterCapture capture)
    {
        var suffix = capture.IsReplay ? "replay" : "capture";
        return $"pico-gb-printer-{DateTime.Now:yyyyMMdd-HHmmss}-{suffix}.bin";
    }

    private static void PrintStatus(PicoGbPrinterStatus status)
    {
        LogInfo($"Queued captures: {status.QueuedCaptures}");
        LogInfo($"Last capture size: {status.LastCaptureSize}");
        LogInfo($"Replay capture size: {status.ReplayCaptureSize}");
        LogInfo($"Printing: {status.IsPrinting}");
        LogInfo($"Replay available: {status.HasReplayCapture}");
        LogInfo($"Debug enabled: {status.DebugEnabled}");
    }

    private static void ListPorts()
    {
        var ports = PicoGbPrinterSerialClient.GetAvailablePortNames();
        if (ports.Length == 0)
        {
            LogWarning("No serial ports found.");
            return;
        }

        foreach (var port in ports) Console.WriteLine(port);
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Pico GB Printer serial console");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  list-ports");
        Console.WriteLine("  probe [--port COM6]");
        Console.WriteLine("  status [--port COM6]");
        Console.WriteLine("  wait-next [--port COM6] [--output capture.bin]");
        Console.WriteLine("  get-next [--port COM6] [--output capture.bin]");
        Console.WriteLine("  get-last [--port COM6] [--output capture.bin]");
        Console.WriteLine("  clear [--port COM6]");
        Console.WriteLine("  debug --enabled true|false [--port COM6]");
        Console.WriteLine("  click --mask 0x10 [--port COM6]");
    }

    private static void LogInfo(string message) => Log("INFO", message, Console.Out);

    private static void LogWarning(string message) => Log("WARN", message, Console.Out);

    private static void LogError(string message) => Log("ERROR", message, Console.Error);

    private static void Log(string level, string message, TextWriter writer)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";
        writer.WriteLine(line);
        Debug.WriteLine(line);
    }
}

internal sealed class CommandArguments
{
    private readonly Dictionary<string, string?> _options;

    private CommandArguments(string commandName, Dictionary<string, string?> options)
    {
        CommandName = commandName;
        _options = options;
    }

    public string CommandName { get; }

    public static CommandArguments Parse(string[] args)
    {
        var commandName = args.Length == 0 ? string.Empty : args[0];
        var options = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        for (var index = 1; index < args.Length; index++)
        {
            var arg = args[index];
            if (!arg.StartsWith("--", StringComparison.Ordinal)) throw new CommandLineException($"Unexpected argument '{arg}'.");

            var name = arg.Substring(2);
            string? value = null;
            if (index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                value = args[++index];
            }

            options[name] = value;
        }

        return new CommandArguments(commandName, options);
    }

    public bool HasFlag(string optionName)
    {
        return _options.ContainsKey(optionName);
    }

    public string? GetOptionalString(string optionName)
    {
        return _options.TryGetValue(optionName, out var value) ? value : null;
    }

    public int GetRequiredInt(string optionName)
    {
        var value = GetRequiredString(optionName);
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) return Convert.ToInt32(value, 16);
        if (int.TryParse(value, out var parsed)) return parsed;
        throw new CommandLineException($"--{optionName} must be an integer.");
    }

    public bool GetRequiredBool(string optionName)
    {
        var value = GetRequiredString(optionName);
        if (bool.TryParse(value, out var parsed)) return parsed;
        if (value == "1") return true;
        if (value == "0") return false;
        throw new CommandLineException($"--{optionName} must be true or false.");
    }

    private string GetRequiredString(string optionName)
    {
        var value = GetOptionalString(optionName);
        if (string.IsNullOrWhiteSpace(value)) throw new CommandLineException($"Missing required --{optionName} value.");
        return value;
    }
}

internal sealed class CommandLineException : Exception
{
    public CommandLineException(string message)
        : base(message)
    {
    }
}
