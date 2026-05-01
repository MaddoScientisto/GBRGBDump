using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace GBTools.GBxCart.Serial.Serial;

internal sealed class SystemIoGbxCartSerialConnection : IGbxCartSerialConnection
{
    private readonly GbxCartClientOptions _options;
    private SerialPort? _serialPort;
    private BufferedSerialReader? _reader;

    public SystemIoGbxCartSerialConnection(GbxCartClientOptions options)
    {
        _options = options;
    }

    public string PortName => _serialPort?.PortName ?? _options.PortName;

    public bool IsOpen => _serialPort is { IsOpen: true };

    public void Open()
    {
        _serialPort = new SerialPort(_options.PortName, _options.BaudRate, Parity.None, 8, StopBits.One)
        {
            Handshake = Handshake.None,
            ReadTimeout = _options.PortReadTimeoutMs,
            WriteTimeout = _options.PortWriteTimeoutMs,
            DtrEnable = false,
            RtsEnable = false,
        };
        _serialPort.Open();
        _serialPort.DiscardInBuffer();
        _serialPort.DiscardOutBuffer();
        _reader = new BufferedSerialReader(_serialPort, GbxCartProtocolConstants.MaxTransferSize);
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
        _reader?.Clear();

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
        return _reader!.ReadExactAsync(count, commandTimeoutMs, cancellationToken);
    }

    public void Dispose()
    {
        _reader?.Dispose();
        _reader = null;
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