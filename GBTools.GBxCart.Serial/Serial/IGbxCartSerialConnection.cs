using System;
using System.Threading;
using System.Threading.Tasks;

namespace GBTools.GBxCart.Serial.Serial;

internal interface IGbxCartSerialConnection : IDisposable
{
    string PortName { get; }

    bool IsOpen { get; }

    void Open();

    void Close();

    void DiscardInBuffer();

    void DiscardOutBuffer();

    void Write(byte[] buffer, int offset, int count);

    byte[] ReadAvailableBytes();

    Task<byte[]> ReadExactAsync(int count, int commandTimeoutMs, int portReadTimeoutMs, CancellationToken cancellationToken);
}