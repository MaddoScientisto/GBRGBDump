#if NET8_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RjcpParity = RJCP.IO.Ports.Parity;
using RjcpSerialPortStream = RJCP.IO.Ports.SerialPortStream;
using RjcpStopBits = RJCP.IO.Ports.StopBits;
using RjcpHandshake = RJCP.IO.Ports.Handshake;

namespace GBTools.GBxCart.Serial.Serial;

internal sealed class RjcpGbxCartSerialConnection : IGbxCartSerialConnection
{
    private readonly GbxCartClientOptions _options;
    private RjcpSerialPortStream? _serialPort;

    public RjcpGbxCartSerialConnection(GbxCartClientOptions options)
    {
        _options = options;
    }

    public string PortName => _serialPort?.PortName ?? _options.PortName;

    public bool IsOpen => _serialPort is { IsOpen: true };

    public void Open()
    {
        _serialPort = new RjcpSerialPortStream(_options.PortName, _options.BaudRate, 8, RjcpParity.None, RjcpStopBits.One)
        {
            Handshake = RjcpHandshake.None,
            ReadTimeout = _options.PortReadTimeoutMs,
            WriteTimeout = _options.PortWriteTimeoutMs,
            ThrowOnReadError = true,
            DtrEnable = false,
            RtsEnable = false,
        };
        _serialPort.Open();
        _serialPort.DiscardInBuffer();
        _serialPort.DiscardOutBuffer();
    }

    public void Close()
    {
        if (_serialPort is { IsOpen: true })
        {
            _serialPort.Close();
        }
    }

    public void DiscardInBuffer()
    {
        try
        {
            _serialPort?.DiscardInBuffer();
        }
        catch
        {
        }
    }

    public void DiscardOutBuffer()
    {
        try
        {
            _serialPort?.DiscardOutBuffer();
        }
        catch
        {
        }
    }

    public void Write(byte[] buffer, int offset, int count)
    {
        EnsureOpen();
        _serialPort!.Write(buffer, offset, count);
        _serialPort.Flush();
    }

    public byte[] ReadAvailableBytes()
    {
        EnsureOpen();

        List<byte> bytes = new List<byte>();
        byte[] buffer = new byte[64];

        while (_serialPort!.IsOpen)
        {
            int available = _serialPort.BytesToRead;
            if (available <= 0)
            {
                break;
            }

            int bytesRead;
            try
            {
                bytesRead = _serialPort.Read(buffer, 0, Math.Min(buffer.Length, available));
            }
            catch (TimeoutException)
            {
                break;
            }

            if (bytesRead <= 0)
            {
                break;
            }

            for (int index = 0; index < bytesRead; index++)
            {
                bytes.Add(buffer[index]);
            }
        }

        return bytes.ToArray();
    }

    public Task<byte[]> ReadExactAsync(int count, int commandTimeoutMs, int portReadTimeoutMs, CancellationToken cancellationToken)
    {
        EnsureOpen();

        byte[] buffer = new byte[count];
        int offset = 0;
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(commandTimeoutMs);

        while (offset < count)
        {
            cancellationToken.ThrowIfCancellationRequested();

            TimeSpan remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                throw new TimeoutException($"Timed out waiting for {count} byte(s); received {offset}.");
            }

            _serialPort!.ReadTimeout = Math.Max(1, Math.Min(portReadTimeoutMs, (int)remaining.TotalMilliseconds));
            int bytesRead = _serialPort.Read(buffer, offset, count - offset);
            if (bytesRead <= 0)
            {
                continue;
            }

            offset += bytesRead;
        }

        return Task.FromResult(buffer);
    }

    public void Dispose()
    {
        _serialPort?.Dispose();
        _serialPort = null;
    }

    private void EnsureOpen()
    {
        if (!IsOpen)
        {
            throw new InvalidOperationException("The serial port is not open.");
        }
    }
}
#endif