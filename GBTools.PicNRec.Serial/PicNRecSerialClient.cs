using System;
using System.Globalization;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GBTools.PicNRec.Serial;

public sealed class PicNRecSerialClient : IDisposable
{
    private readonly SemaphoreSlim _operationLock = new SemaphoreSlim(1, 1);
    private readonly PicNRecClientOptions _options;

    private SerialPort? _serialPort;
    private BufferedSerialReader? _reader;
    private int _currentBaudRate;
    private bool _disposed;

    public PicNRecSerialClient(PicNRecClientOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public string PortName => _serialPort?.PortName ?? _options.PortName;

    public int CurrentBaudRate => _currentBaudRate;

    public bool IsConnected => _serialPort != null && _serialPort.IsOpen;

    public static string[] GetAvailablePortNames()
    {
        return SerialPort.GetPortNames()
            .OrderBy(static portName => portName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsConnected)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_options.PortName))
            {
                throw new InvalidOperationException("A serial port name is required.");
            }

            OpenPort(_options.PortName, PicNRecProtocolConstants.DefaultBaudRate);
            await FlushInputCoreAsync(cancellationToken).ConfigureAwait(false);

            if (_options.ConnectInFastMode)
            {
                await EnterFastModeCoreAsync(cancellationToken).ConfigureAwait(false);
            }
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
            ClosePort();
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<int> EnterFastModeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await EnterFastModeCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<byte[]> ReadMetadataAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureConnected();
            Exception? lastError = null;
            for (var attempt = 1; attempt <= _options.MetadataReadRetryCount; attempt++)
            {
                try
                {
                    await FlushInputCoreAsync(cancellationToken).ConfigureAwait(false);
                    await WriteNumberCommandCoreAsync(0, cancellationToken).ConfigureAwait(false);
                    await WriteModeCommandCoreAsync('R', cancellationToken).ConfigureAwait(false);

                    var metadata = await ReadBlockSequenceCoreAsync(
                        PicNRecProtocolConstants.MetadataBlockCount,
                        cancellationToken).ConfigureAwait(false);
                    await WriteModeCommandCoreAsync('0', cancellationToken).ConfigureAwait(false);
                    return metadata;
                }
                catch (Exception error)
                {
                    lastError = error;
                    await SafeStopCoreAsync(cancellationToken).ConfigureAwait(false);
                    await FlushInputCoreAsync(cancellationToken).ConfigureAwait(false);

                    if (attempt >= _options.MetadataReadRetryCount)
                    {
                        break;
                    }

                    await Task.Delay(_options.MetadataReadRetryDelayMs, cancellationToken).ConfigureAwait(false);
                }
            }

            throw new InvalidOperationException(
                $"Metadata read failed after {_options.MetadataReadRetryCount} attempts.",
                lastError);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<int> ReadLastImageNumberAsync(CancellationToken cancellationToken = default)
    {
        var metadata = await ReadMetadataAsync(cancellationToken).ConfigureAwait(false);
        return PicNRecBinary.DecodeLastImageNumber(metadata);
    }

    public async Task<byte[]> ReadImageAsync(int imageNumber, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (imageNumber < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(imageNumber));
        }

        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureConnected();

            Exception? lastError = null;
            for (var attempt = 1; attempt <= _options.ImageReadRetryCount; attempt++)
            {
                try
                {
                    await FlushInputCoreAsync(cancellationToken).ConfigureAwait(false);
                    await WriteNumberCommandCoreAsync(imageNumber, cancellationToken).ConfigureAwait(false);
                    await WriteModeCommandCoreAsync('R', cancellationToken).ConfigureAwait(false);

                    var image = await ReadBlockSequenceCoreAsync(
                        PicNRecProtocolConstants.ImageBlockCount,
                        cancellationToken).ConfigureAwait(false);

                    await WriteModeCommandCoreAsync('0', cancellationToken).ConfigureAwait(false);
                    return image;
                }
                catch (Exception error)
                {
                    lastError = error;
                    await SafeStopCoreAsync(cancellationToken).ConfigureAwait(false);
                    await FlushInputCoreAsync(cancellationToken).ConfigureAwait(false);

                    if (attempt >= _options.ImageReadRetryCount)
                    {
                        break;
                    }

                    await Task.Delay(_options.ImageReadRetryDelayMs, cancellationToken).ConfigureAwait(false);
                }
            }

            throw new InvalidOperationException(
                $"Image read failed after {_options.ImageReadRetryCount} attempts.",
                lastError);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task ClearMetadataAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureConnected();
            await FlushInputCoreAsync(cancellationToken).ConfigureAwait(false);
            await WriteAsciiCoreAsync("k", cancellationToken).ConfigureAwait(false);
            var ack = await _reader!.ReadExactAsync(1, _options.BlockTimeoutMs, cancellationToken).ConfigureAwait(false);

            if (ack[0] != 0x31)
            {
                throw new InvalidOperationException(
                    $"Unexpected clear acknowledgement: 0x{ack[0].ToString("x2", CultureInfo.InvariantCulture)}");
            }
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ClosePort();
        _operationLock.Dispose();
    }

    private void OpenPort(string portName, int baudRate)
    {
        ClosePort();

        var serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
        {
            Handshake = Handshake.None,
            DtrEnable = _options.EnableDtr,
            RtsEnable = _options.EnableRts,
            ReadTimeout = _options.PortReadTimeoutMs,
            WriteTimeout = _options.PortWriteTimeoutMs,
            Encoding = Encoding.ASCII,
        };

        serialPort.Open();
        _serialPort = serialPort;
        _reader = new BufferedSerialReader(serialPort, _options.ReadBufferSize);
        _currentBaudRate = baudRate;
    }

    private void ClosePort()
    {
        _reader?.Dispose();
        _reader = null;

        if (_serialPort != null)
        {
            try
            {
                if (_serialPort.IsOpen)
                {
                    _serialPort.Close();
                }
            }
            finally
            {
                _serialPort.Dispose();
                _serialPort = null;
                _currentBaudRate = 0;
            }
        }
    }

    private async Task<int> EnterFastModeCoreAsync(CancellationToken cancellationToken)
    {
        EnsureConnected();
        await FlushInputCoreAsync(cancellationToken).ConfigureAwait(false);
        await WriteAsciiCoreAsync(">", cancellationToken).ConfigureAwait(false);
        await Task.Delay(_options.FastModeSettleDelayMs, cancellationToken).ConfigureAwait(false);

        var portName = PortName;
        ClosePort();
        await Task.Delay(_options.FastModeSettleDelayMs, cancellationToken).ConfigureAwait(false);

        try
        {
            OpenPort(portName, PicNRecProtocolConstants.FastBaudRate);
        }
        catch
        {
            OpenPort(portName, PicNRecProtocolConstants.DefaultBaudRate);
        }

        await FlushInputCoreAsync(cancellationToken).ConfigureAwait(false);
        return _currentBaudRate;
    }

    private async Task<byte[]> ReadBlockSequenceCoreAsync(int blockCount, CancellationToken cancellationToken)
    {
        var output = new byte[blockCount * PicNRecProtocolConstants.BlockSize];
        var offset = 0;

        for (var blockIndex = 0; blockIndex < blockCount; blockIndex++)
        {
            var block = await _reader!
                .ReadExactAsync(PicNRecProtocolConstants.BlockSize, _options.BlockTimeoutMs, cancellationToken)
                .ConfigureAwait(false);

            Buffer.BlockCopy(block, 0, output, offset, block.Length);
            offset += block.Length;

            if (blockIndex < blockCount - 1)
            {
                await WriteModeCommandCoreAsync('1', cancellationToken).ConfigureAwait(false);
            }
        }

        return output;
    }

    private async Task FlushInputCoreAsync(CancellationToken cancellationToken)
    {
        EnsureConnected();
        DiscardInBufferSafe();
        _reader!.Clear();

        var deadline = DateTime.UtcNow.AddMilliseconds(_options.FlushMaxDrainMs);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                await _reader.WaitForSignalAsync(_options.FlushSilenceMs, cancellationToken).ConfigureAwait(false);
                DiscardInBufferSafe();
                _reader.Clear();
            }
            catch (TimeoutException)
            {
                return;
            }
        }
    }

    private Task WriteModeCommandCoreAsync(char command, CancellationToken cancellationToken)
    {
        return WriteAsciiCoreAsync(command.ToString(), cancellationToken);
    }

    private Task WriteNumberCommandCoreAsync(int number, CancellationToken cancellationToken)
    {
        var value = number.ToString("x", CultureInfo.InvariantCulture);
        var text = "A" + value;
        var payload = Encoding.ASCII.GetBytes(text);
        var output = new byte[payload.Length + 1];
        Buffer.BlockCopy(payload, 0, output, 0, payload.Length);
        output[output.Length - 1] = 0x00;
        return WriteBytesCoreAsync(output, cancellationToken);
    }

    private Task WriteAsciiCoreAsync(string text, CancellationToken cancellationToken)
    {
        return WriteBytesCoreAsync(Encoding.ASCII.GetBytes(text), cancellationToken);
    }

    private Task WriteBytesCoreAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureConnected();
        _serialPort!.Write(bytes, 0, bytes.Length);
        return Task.CompletedTask;
    }

    private Task SafeStopCoreAsync(CancellationToken cancellationToken)
    {
        if (!IsConnected)
        {
            return Task.CompletedTask;
        }

        try
        {
            return WriteModeCommandCoreAsync('0', cancellationToken);
        }
        catch
        {
            return Task.CompletedTask;
        }
    }

    private void DiscardInBufferSafe()
    {
        try
        {
            _serialPort!.DiscardInBuffer();
        }
        catch
        {
        }
    }

    private void EnsureConnected()
    {
        if (!IsConnected || _reader == null)
        {
            throw new InvalidOperationException("Serial device is not connected.");
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(PicNRecSerialClient));
        }
    }
}