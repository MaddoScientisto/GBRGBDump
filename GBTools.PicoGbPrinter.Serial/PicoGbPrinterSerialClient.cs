using System;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GBTools.PicoGbPrinter.Serial;

public sealed class PicoGbPrinterSerialClient : IDisposable
{
    private readonly SemaphoreSlim _operationLock = new SemaphoreSlim(1, 1);
    private readonly PicoGbPrinterClientOptions _options;

    private SerialPort? _serialPort;
    private BufferedSerialReader? _reader;
    private bool _disposed;

    public PicoGbPrinterSerialClient(PicoGbPrinterClientOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public string PortName => _serialPort?.PortName ?? _options.PortName;

    public bool IsConnected => _serialPort != null && _serialPort.IsOpen;

    public static string[] GetAvailablePortNames()
    {
        return SerialPort.GetPortNames()
            .OrderBy(static portName => GetPortPriority(portName))
            .ThenBy(static portName => portName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static async Task<PicoGbPrinterPortInfo?> TryAutoDetectAsync(CancellationToken cancellationToken = default)
    {
        foreach (var portName in GetAvailablePortNames())
        {
            if (await TryProbeAsync(portName, cancellationToken).ConfigureAwait(false))
            {
                return new PicoGbPrinterPortInfo(portName, true);
            }
        }

        return null;
    }

    public static async Task<bool> TryProbeAsync(string portName, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new PicoGbPrinterSerialClient(new PicoGbPrinterClientOptions { PortName = portName });
            await client.ConnectAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsConnected) return;
            if (string.IsNullOrWhiteSpace(_options.PortName)) throw new InvalidOperationException("A serial port name is required.");

            Trace($"Opening serial port {_options.PortName} at {_options.BaudRate} baud.");

            _serialPort = new SerialPort(_options.PortName, _options.BaudRate, Parity.None, 8, StopBits.One)
            {
                Handshake = Handshake.None,
                ReadTimeout = SerialPort.InfiniteTimeout,
                WriteTimeout = _options.PortWriteTimeoutMs,
                DtrEnable = _options.EnableDtr,
                RtsEnable = _options.EnableRts,
            };
            _serialPort.Open();
            _serialPort.DiscardOutBuffer();
            _reader = new BufferedSerialReader(_serialPort, _options.ReadBufferSize);

            await ReadBannerAsync(cancellationToken).ConfigureAwait(false);
            Trace($"Connected to {_options.PortName}.");
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsConnected)
            {
                Trace($"Closing serial port {PortName}.");
            }

            ClosePort();
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<PicoGbPrinterStatus> ReadStatusAsync(CancellationToken cancellationToken = default)
    {
        return ParseStatus(await SendCommandForFrameAsync("STATUS", PicoGbPrinterProtocolConstants.StatusFrame, _options.CommandTimeoutMs, cancellationToken).ConfigureAwait(false));
    }

    public async Task<PicoGbPrinterCapture?> GetNextCaptureAsync(CancellationToken cancellationToken = default)
    {
        var frame = await SendCommandForFrameAsync("GET_NEXT", null, _options.CommandTimeoutMs, cancellationToken).ConfigureAwait(false);
        return frame.Type == PicoGbPrinterProtocolConstants.JobFrame ? new PicoGbPrinterCapture(frame.Payload, frame.Flags) : null;
    }

    public async Task<PicoGbPrinterCapture?> GetLastCaptureAsync(CancellationToken cancellationToken = default)
    {
        var frame = await SendCommandForFrameAsync("GET_LAST", null, _options.CommandTimeoutMs, cancellationToken).ConfigureAwait(false);
        return frame.Type == PicoGbPrinterProtocolConstants.JobFrame ? new PicoGbPrinterCapture(frame.Payload, frame.Flags) : null;
    }

    public async Task<PicoGbPrinterCapture> WaitForNextCaptureAsync(IProgress<long>? progress = null, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureConnected();
            Trace("Waiting for the next live capture from the Pico GB Printer.");
            while (true)
            {
                var frame = await ReadFrameCoreAsync(_options.CaptureTimeoutMs, progress, cancellationToken).ConfigureAwait(false);
                if (frame.Type == PicoGbPrinterProtocolConstants.JobFrame)
                {
                    Trace($"Received live capture frame with {frame.Payload.Length} bytes.");
                    return new PicoGbPrinterCapture(frame.Payload, frame.Flags);
                }

                ThrowIfErrorFrame(frame);
            }
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<PicoGbPrinterCapture> CaptureUntilStoppedAsync(
        Func<PicoGbPrinterCapture, Task>? onCaptureReceived = null,
        Func<bool>? shouldStop = null,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureConnected();

            using MemoryStream combinedPayload = new();
            ushort combinedFlags = 0;
            var captureCount = 0;
            var stopRequested = false;
            var receivedAnyCapture = false;

            async Task HandleCapturedFrameAsync(PicoGbPrinterFrame frame)
            {
                captureCount++;
                receivedAnyCapture = true;
                combinedFlags |= frame.Flags;
                combinedPayload.Write(frame.Payload, 0, frame.Payload.Length);
                Trace($"Captured session frame {captureCount} with {frame.Payload.Length} bytes. Session total: {combinedPayload.Length} bytes.");

                if (onCaptureReceived is not null)
                {
                    await onCaptureReceived(new PicoGbPrinterCapture((byte[])frame.Payload.Clone(), frame.Flags)).ConfigureAwait(false);
                }
            }

            Trace("Waiting for live Pico GB Printer captures until stop is requested.");

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!stopRequested && shouldStop?.Invoke() == true)
                {
                    stopRequested = true;
                    Trace("Stop requested. Draining any in-flight capture frames before finishing the session.");
                }

                PicoGbPrinterFrame? frame = await TryReadFrameCoreAsync(_options.CaptureIdleTimeoutMs, progress, cancellationToken).ConfigureAwait(false);
                if (frame is null)
                {
                    if (captureCount > 0 && !stopRequested)
                    {
                        Trace($"No additional capture frames arrived for {_options.CaptureIdleTimeoutMs} ms. Treating the current burst as complete and clearing confirmed device buffers.");
                        await SendCommandForFrameCoreAsync(
                            "CLEAR",
                            PicoGbPrinterProtocolConstants.AckFrame,
                            _options.CommandTimeoutMs,
                            cancellationToken,
                            async unexpectedFrame =>
                            {
                                if (unexpectedFrame.Type != PicoGbPrinterProtocolConstants.JobFrame)
                                {
                                    return false;
                                }

                                Trace("Received a late JOB frame while clearing an idle burst; keeping it and waiting for CLEAR acknowledgement.");
                                await HandleCapturedFrameAsync(unexpectedFrame).ConfigureAwait(false);
                                return true;
                            }).ConfigureAwait(false);
                        Trace("Cleared device queue and replay buffer after the idle burst was acknowledged.");

                        captureCount = 0;
                        continue;
                    }

                    if (stopRequested)
                    {
                        if (!receivedAnyCapture)
                        {
                            Trace("Stop requested before any live capture arrived.");
                            throw new OperationCanceledException("Capture stopped before any Pico GB Printer data was received.", cancellationToken);
                        }

                        Trace($"Stop requested after receiving {combinedPayload.Length} total session bytes.");
                        return new PicoGbPrinterCapture(combinedPayload.ToArray(), combinedFlags);
                    }

                    continue;
                }

                if (frame.Type == PicoGbPrinterProtocolConstants.HelloFrame)
                {
                    Trace("< HELLO");
                    continue;
                }

                if (frame.Type == PicoGbPrinterProtocolConstants.JobFrame)
                {
                    await HandleCapturedFrameAsync(frame).ConfigureAwait(false);
                    continue;
                }

                ThrowIfErrorFrame(frame);
                Trace($"Ignoring unexpected frame 0x{frame.Type:x2} while live capture is active.");
            }
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await SendCommandForFrameAsync("CLEAR", PicoGbPrinterProtocolConstants.AckFrame, _options.CommandTimeoutMs, cancellationToken).ConfigureAwait(false);
    }

    public async Task SetDebugAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        await SendCommandForFrameAsync(enabled ? "DEBUG 1" : "DEBUG 0", PicoGbPrinterProtocolConstants.StatusFrame, _options.CommandTimeoutMs, cancellationToken).ConfigureAwait(false);
    }

    public async Task ClickAsync(byte keyMask, CancellationToken cancellationToken = default)
    {
        await SendCommandForFrameAsync("CLICK 0x" + keyMask.ToString("x2", CultureInfo.InvariantCulture), PicoGbPrinterProtocolConstants.AckFrame, _options.CommandTimeoutMs, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ClosePort();
        _operationLock.Dispose();
    }

    private async Task<PicoGbPrinterFrame> SendCommandForFrameAsync(string command, byte? expectedFrameType, int timeoutMs, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await SendCommandForFrameCoreAsync(command, expectedFrameType, timeoutMs, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    private async Task ReadBannerAsync(CancellationToken cancellationToken)
    {
        var line = new StringBuilder();
        var deadline = DateTime.UtcNow.AddMilliseconds(_options.BannerTimeoutMs);

        while (DateTime.UtcNow < deadline)
        {
            var remaining = Math.Max(1, (int)(deadline - DateTime.UtcNow).TotalMilliseconds);
            var data = await _reader!.ReadExactAsync(1, remaining, cancellationToken).ConfigureAwait(false);
            var ch = (char)data[0];
            if (ch == '\r') continue;
            if (ch == '\n')
            {
                string bannerLine = line.ToString();
                if (bannerLine.Length > 0)
                {
                    Trace($"< {bannerLine}");
                }

                if (bannerLine.IndexOf(PicoGbPrinterProtocolConstants.Banner, StringComparison.Ordinal) >= 0) return;
                line.Clear();
                continue;
            }

            if (line.Length < 128) line.Append(ch);
        }

        throw new TimeoutException("Timed out waiting for Pico GB Printer serial banner.");
    }

    private async Task<PicoGbPrinterFrame> ReadFrameCoreAsync(int timeoutMs, IProgress<long>? progress, CancellationToken cancellationToken)
    {
        await ReadMagicAsync(timeoutMs, cancellationToken).ConfigureAwait(false);
        var headerRest = await _reader!.ReadExactAsync(PicoGbPrinterProtocolConstants.FrameHeaderLength - 4, timeoutMs, cancellationToken).ConfigureAwait(false);

        if (headerRest[0] != PicoGbPrinterProtocolConstants.FrameVersion)
        {
            throw new InvalidOperationException($"Unsupported Pico GB Printer frame version {headerRest[0]}.");
        }

        var type = headerRest[1];
        var flags = ReadUInt16(headerRest, 2);
        var length = ReadUInt32(headerRest, 4);
        if (length > int.MaxValue)
        {
            throw new InvalidOperationException($"Frame payload is too large: {length} bytes.");
        }

        Trace($"Receiving frame 0x{type:x2} with {length} payload bytes.");

        var payload = length == 0
            ? Array.Empty<byte>()
            : await _reader.ReadExactAsync((int)length, timeoutMs, cancellationToken).ConfigureAwait(false);
        progress?.Report(payload.Length);

        var crcBytes = await _reader.ReadExactAsync(PicoGbPrinterProtocolConstants.FrameCrcLength, timeoutMs, cancellationToken).ConfigureAwait(false);
        var expectedCrc = ReadUInt32(crcBytes, 0);
        var actualCrc = Crc32(payload);
        if (expectedCrc != actualCrc)
        {
            throw new InvalidOperationException($"Frame CRC mismatch. Expected 0x{expectedCrc:x8}, got 0x{actualCrc:x8}.");
        }

        Trace($"Validated frame 0x{type:x2} with CRC 0x{actualCrc:x8}.");

        return new PicoGbPrinterFrame(type, flags, payload);
    }

    private async Task<PicoGbPrinterFrame?> TryReadFrameCoreAsync(int timeoutMs, IProgress<long>? progress, CancellationToken cancellationToken)
    {
        bool foundMagic = await TryReadMagicAsync(timeoutMs, cancellationToken).ConfigureAwait(false);
        if (!foundMagic)
        {
            return null;
        }

        var headerRest = await _reader!.ReadExactAsync(PicoGbPrinterProtocolConstants.FrameHeaderLength - 4, timeoutMs, cancellationToken).ConfigureAwait(false);

        if (headerRest[0] != PicoGbPrinterProtocolConstants.FrameVersion)
        {
            throw new InvalidOperationException($"Unsupported Pico GB Printer frame version {headerRest[0]}.");
        }

        var type = headerRest[1];
        var flags = ReadUInt16(headerRest, 2);
        var length = ReadUInt32(headerRest, 4);
        if (length > int.MaxValue)
        {
            throw new InvalidOperationException($"Frame payload is too large: {length} bytes.");
        }

        Trace($"Receiving frame 0x{type:x2} with {length} payload bytes.");

        var payload = length == 0
            ? Array.Empty<byte>()
            : await _reader.ReadExactAsync((int)length, timeoutMs, cancellationToken).ConfigureAwait(false);
        progress?.Report(payload.Length);

        var crcBytes = await _reader.ReadExactAsync(PicoGbPrinterProtocolConstants.FrameCrcLength, timeoutMs, cancellationToken).ConfigureAwait(false);
        var expectedCrc = ReadUInt32(crcBytes, 0);
        var actualCrc = Crc32(payload);
        if (expectedCrc != actualCrc)
        {
            throw new InvalidOperationException($"Frame CRC mismatch. Expected 0x{expectedCrc:x8}, got 0x{actualCrc:x8}.");
        }

        Trace($"Validated frame 0x{type:x2} with CRC 0x{actualCrc:x8}.");

        return new PicoGbPrinterFrame(type, flags, payload);
    }

    private async Task ReadMagicAsync(int timeoutMs, CancellationToken cancellationToken)
    {
        var matched = 0;
        var magic = new byte[] { (byte)'P', (byte)'G', (byte)'B', (byte)'S' };

        while (matched < magic.Length)
        {
            var data = await _reader!.ReadExactAsync(1, timeoutMs, cancellationToken).ConfigureAwait(false);
            if (data[0] == magic[matched])
            {
                matched++;
            }
            else
            {
                matched = data[0] == magic[0] ? 1 : 0;
            }
        }
    }

    private async Task<bool> TryReadMagicAsync(int timeoutMs, CancellationToken cancellationToken)
    {
        var matched = 0;
        var magic = new byte[] { (byte)'P', (byte)'G', (byte)'B', (byte)'S' };
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

        while (matched < magic.Length)
        {
            int remainingMs = Math.Max(1, (int)(deadline - DateTime.UtcNow).TotalMilliseconds);
            if (remainingMs <= 0)
            {
                return false;
            }

            var data = await _reader!.TryReadExactAsync(1, remainingMs, cancellationToken).ConfigureAwait(false);
            if (data is null)
            {
                return false;
            }

            if (data[0] == magic[matched])
            {
                matched++;
            }
            else
            {
                matched = data[0] == magic[0] ? 1 : 0;
            }
        }

        return true;
    }

    private static PicoGbPrinterStatus ParseStatus(PicoGbPrinterFrame frame)
    {
        if (frame.Payload.Length < 16) throw new InvalidOperationException("Status frame is too short.");
        return new PicoGbPrinterStatus(
            ReadUInt32(frame.Payload, 0),
            ReadUInt32(frame.Payload, 4),
            ReadUInt32(frame.Payload, 8),
            frame.Payload[12] != 0,
            frame.Payload[13] != 0,
            frame.Payload[14] != 0);
    }

    private static void ThrowIfErrorFrame(PicoGbPrinterFrame frame)
    {
        if (frame.Type != PicoGbPrinterProtocolConstants.ErrorFrame) return;
        var message = Encoding.ASCII.GetString(frame.Payload);
        throw new InvalidOperationException("Pico GB Printer error: " + message);
    }

    private static ushort ReadUInt16(byte[] data, int offset)
    {
        return (ushort)(data[offset] | (data[offset + 1] << 8));
    }

    private static uint ReadUInt32(byte[] data, int offset)
    {
        return (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));
    }

    private static uint Crc32(byte[] data)
    {
        uint crc = 0xffffffffu;
        for (var index = 0; index < data.Length; index++)
        {
            crc ^= data[index];
            for (var bit = 0; bit < 8; bit++)
            {
                var mask = 0u - (crc & 1u);
                crc = (crc >> 1) ^ (0xedb88320u & mask);
            }
        }

        return ~crc;
    }

    private static int GetPortPriority(string portName)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return 0;
        if (portName.StartsWith("/dev/ttyACM", StringComparison.OrdinalIgnoreCase)) return 0;
        if (portName.StartsWith("/dev/ttyUSB", StringComparison.OrdinalIgnoreCase)) return 1;
        return 2;
    }

    private void EnsureConnected()
    {
        if (!IsConnected || _reader == null) throw new InvalidOperationException("The serial port is not open.");
    }

    private void ClosePort()
    {
        _reader?.Dispose();
        _reader = null;
        if (_serialPort != null)
        {
            try
            {
                if (_serialPort.IsOpen) _serialPort.Close();
            }
            finally
            {
                _serialPort.Dispose();
                _serialPort = null;
            }
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(PicoGbPrinterSerialClient));
    }

    private async Task<PicoGbPrinterFrame> SendCommandForFrameCoreAsync(
        string command,
        byte? expectedFrameType,
        int timeoutMs,
        CancellationToken cancellationToken,
        Func<PicoGbPrinterFrame, Task<bool>>? unexpectedFrameHandler = null)
    {
        EnsureConnected();
        Trace($"> {command}");
        var bytes = Encoding.ASCII.GetBytes(command + "\n");
        _serialPort!.Write(bytes, 0, bytes.Length);

        while (true)
        {
            var frame = await ReadFrameCoreAsync(timeoutMs, null, cancellationToken).ConfigureAwait(false);
            ThrowIfErrorFrame(frame);
            if (frame.Type == PicoGbPrinterProtocolConstants.HelloFrame)
            {
                Trace("< HELLO");
                continue;
            }

            if (expectedFrameType == null || frame.Type == expectedFrameType.Value)
            {
                Trace($"< frame 0x{frame.Type:x2} ({frame.Payload.Length} bytes)");
                return frame;
            }

            if (unexpectedFrameHandler is not null && await unexpectedFrameHandler(frame).ConfigureAwait(false))
            {
                continue;
            }

            Trace($"< frame 0x{frame.Type:x2} ignored while waiting for 0x{expectedFrameType:x2}");
        }
    }

    private void Trace(string message)
    {
        _options.Trace?.Report(message);
    }
}
