using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace GBTools.PicoGbPrinter.Serial;

internal sealed class BufferedSerialReader : IDisposable
{
    private readonly object _sync = new object();
    private readonly object _drainSync = new object();
    private readonly List<TaskCompletionSource<object>> _waiters = new List<TaskCompletionSource<object>>();
    private readonly Queue<ArraySegment<byte>> _chunks = new Queue<ArraySegment<byte>>();
    private readonly SerialPort _serialPort;
    private readonly int _readChunkSize;

    private Exception? _fault;
    private int _bufferedBytes;
    private bool _completed;
    private bool _disposed;
    private bool _isDraining;

    public BufferedSerialReader(SerialPort serialPort, int readChunkSize)
    {
        _serialPort = serialPort ?? throw new ArgumentNullException(nameof(serialPort));
        if (readChunkSize <= 0) throw new ArgumentOutOfRangeException(nameof(readChunkSize));
        _readChunkSize = readChunkSize;
        _serialPort.DataReceived += OnDataReceived;
    }

    public async Task<byte[]> ReadExactAsync(int length, int timeoutMs, CancellationToken cancellationToken)
    {
        if (length <= 0) throw new ArgumentOutOfRangeException(nameof(length));

        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        DrainAvailableBytes();
        var lastObservedBytes = GetBufferedBytes();

        while (GetBufferedBytes() < length)
        {
            ThrowIfFaultedOrCompleted(length);

            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                throw new TimeoutException($"Timed out waiting for {length} bytes; received {GetBufferedBytes()}.");
            }

            await WaitForBufferGrowthAsync(lastObservedBytes, remaining, cancellationToken).ConfigureAwait(false);
            DrainAvailableBytes();
            lastObservedBytes = GetBufferedBytes();
        }

        return Consume(length);
    }

    public async Task<byte[]?> TryReadExactAsync(int length, int timeoutMs, CancellationToken cancellationToken)
    {
        if (length <= 0) throw new ArgumentOutOfRangeException(nameof(length));

        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        DrainAvailableBytes();
        var lastObservedBytes = GetBufferedBytes();

        while (GetBufferedBytes() < length)
        {
            ThrowIfFaultedOrCompleted(length);

            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                return null;
            }

            await WaitForBufferGrowthAsync(lastObservedBytes, remaining, cancellationToken).ConfigureAwait(false);
            DrainAvailableBytes();
            lastObservedBytes = GetBufferedBytes();
        }

        return Consume(length);
    }

    public void Clear()
    {
        lock (_sync)
        {
            _chunks.Clear();
            _bufferedBytes = 0;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            _serialPort.DataReceived -= OnDataReceived;
        }
        catch
        {
        }

        Complete();
    }

    private int GetBufferedBytes()
    {
        lock (_sync)
        {
            return _bufferedBytes;
        }
    }

    private async Task<bool> WaitForBufferGrowthAsync(int previousBufferedBytes, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (GetBufferedBytes() > previousBufferedBytes) return true;

        DrainAvailableBytes();
        if (GetBufferedBytes() > previousBufferedBytes) return true;

        TaskCompletionSource<object> waiter;
        lock (_sync)
        {
            if (_bufferedBytes > previousBufferedBytes) return true;
            if (_fault != null) throw _fault;
            if (_completed) throw new InvalidOperationException("Serial read loop is not running.");

            waiter = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            _waiters.Add(waiter);
        }

        var delayTask = Task.Delay(timeout, cancellationToken);
        var completedTask = await Task.WhenAny(waiter.Task, delayTask).ConfigureAwait(false);

        lock (_sync)
        {
            _waiters.Remove(waiter);
        }

        if (completedTask == delayTask)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return false;
        }

        await waiter.Task.ConfigureAwait(false);
        return true;
    }

    private byte[] Consume(int length)
    {
        var output = new byte[length];
        var written = 0;

        lock (_sync)
        {
            while (written < length)
            {
                var chunk = _chunks.Dequeue();
                var remaining = length - written;

                if (chunk.Count <= remaining)
                {
                    Buffer.BlockCopy(chunk.Array!, chunk.Offset, output, written, chunk.Count);
                    written += chunk.Count;
                    continue;
                }

                Buffer.BlockCopy(chunk.Array!, chunk.Offset, output, written, remaining);
                written += remaining;

                var remainder = new ArraySegment<byte>(chunk.Array!, chunk.Offset + remaining, chunk.Count - remaining);
                var carry = _chunks.ToArray();
                _chunks.Clear();
                _chunks.Enqueue(remainder);

                for (var index = 0; index < carry.Length; index++) _chunks.Enqueue(carry[index]);
                break;
            }

            _bufferedBytes -= length;
        }

        return output;
    }

    private void OnDataReceived(object sender, SerialDataReceivedEventArgs eventArgs)
    {
        try
        {
            DrainAvailableBytes();
        }
        catch (Exception error)
        {
            Fail(error);
        }
    }

    private void DrainAvailableBytes()
    {
        if (_disposed || !_serialPort.IsOpen) return;

        lock (_drainSync)
        {
            if (_isDraining)
            {
                return;
            }

            _isDraining = true;
        }

        byte[] buffer = new byte[_readChunkSize];
        try
        {
            while (!_disposed && _serialPort.IsOpen)
            {
                int availableBytes = _serialPort.BytesToRead;
                if (availableBytes <= 0) return;

                int bytesRead = _serialPort.Read(buffer, 0, Math.Min(buffer.Length, availableBytes));
                if (bytesRead <= 0) return;

                byte[] chunk = new byte[bytesRead];
                Buffer.BlockCopy(buffer, 0, chunk, 0, bytesRead);

                lock (_sync)
                {
                    _chunks.Enqueue(new ArraySegment<byte>(chunk));
                    _bufferedBytes += bytesRead;
                }

                WakeWaiters();
            }
        }
        finally
        {
            lock (_drainSync)
            {
                _isDraining = false;
            }
        }
    }

    private void ThrowIfFaultedOrCompleted(int expectedLength)
    {
        lock (_sync)
        {
            if (_fault != null) throw _fault;
            if (_completed) throw new InvalidOperationException($"Serial read stopped with {_bufferedBytes} of {expectedLength} bytes buffered.");
        }
    }

    private void Fail(Exception error)
    {
        lock (_sync)
        {
            _fault = error;
            _completed = true;
        }

        RejectWaiters(error);
    }

    private void Complete()
    {
        lock (_sync)
        {
            _completed = true;
        }

        WakeWaiters();
    }

    private void WakeWaiters()
    {
        TaskCompletionSource<object>[] waiters;
        lock (_sync)
        {
            waiters = _waiters.ToArray();
        }

        for (var index = 0; index < waiters.Length; index++) waiters[index].TrySetResult(new object());
    }

    private void RejectWaiters(Exception error)
    {
        TaskCompletionSource<object>[] waiters;
        lock (_sync)
        {
            waiters = _waiters.ToArray();
        }

        for (var index = 0; index < waiters.Length; index++) waiters[index].TrySetException(error);
    }
}
