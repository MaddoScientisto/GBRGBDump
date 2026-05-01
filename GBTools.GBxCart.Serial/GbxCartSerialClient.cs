using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GBTools.GBxCart.Serial.Serial;

namespace GBTools.GBxCart.Serial;

public sealed class GbxCartSerialClient : IDisposable
{
    private static readonly IReadOnlyDictionary<string, byte> DeviceCommands = new Dictionary<string, byte>(StringComparer.Ordinal)
    {
        ["QUERY_FW_INFO"] = 0xA1,
        ["OFW_CART_PWR_ON"] = 0x2F,
        ["OFW_CART_PWR_OFF"] = 0x2E,
        ["OFW_QUERY_CART_PWR"] = 0x5D,
        ["CART_PWR_ON"] = 0xF2,
        ["CART_PWR_OFF"] = 0xF3,
        ["SET_MODE_DMG"] = 0xA3,
        ["SET_VOLTAGE_5V"] = 0xA5,
        ["SET_VARIABLE"] = 0xA6,
        ["DMG_CART_READ"] = 0xB1,
        ["DMG_CART_WRITE"] = 0xB2,
    };

    private static readonly IReadOnlyDictionary<string, (int BitWidth, byte Key)> DeviceVariables = new Dictionary<string, (int BitWidth, byte Key)>(StringComparer.Ordinal)
    {
        ["ADDRESS"] = (32, 0x00),
        ["TRANSFER_SIZE"] = (16, 0x00),
        ["CART_MODE"] = (8, 0x00),
        ["DMG_ACCESS_MODE"] = (8, 0x01),
        ["DMG_READ_CS_PULSE"] = (8, 0x08),
        ["DMG_READ_METHOD"] = (8, 0x0B),
    };

    private readonly SemaphoreSlim _operationLock = new SemaphoreSlim(1, 1);
    private readonly GbxCartClientOptions _options;

    private IGbxCartSerialConnection? _serialConnection;
    private int _firmwareVersion;
    private bool _requiresAcknowledgements;
    private bool _dmgReady;
    private bool _disposed;

    public GbxCartSerialClient(GbxCartClientOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public string PortName => _serialConnection?.PortName ?? _options.PortName;

    public bool IsConnected => _serialConnection is { IsOpen: true };

    public static IReadOnlyList<GbxCartPortInfo> GetAvailablePorts() => SerialPortDiscovery.GetAvailablePorts();

    public static async Task<GbxCartProbeResult?> TryAutoDetectAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<GbxCartPortInfo> ports = GetAvailablePorts();
        foreach (GbxCartPortInfo port in ports)
        {
            GbxCartProbeResult? probe = await TryProbeAsync(port.PortName, cancellationToken).ConfigureAwait(false);
            if (probe is not null)
            {
                return probe;
            }
        }

        return null;
    }

    public static async Task<GbxCartProbeResult?> TryProbeAsync(string portName, CancellationToken cancellationToken = default)
    {
        using GbxCartSerialClient client = new(new GbxCartClientOptions { PortName = portName });
        bool probeSucceeded = false;
        try
        {
            await client.ConnectAsync(cancellationToken).ConfigureAwait(false);
            GbxCartCartridgeInfo info = await client.ReadCartridgeInfoAsync(cancellationToken).ConfigureAwait(false);
            probeSucceeded = true;
            return new GbxCartProbeResult(portName, info);
        }
        catch
        {
            return null;
        }
        finally
        {
            if (client.IsConnected)
            {
                if (probeSucceeded)
                {
                    await client.DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
                }
                else
                {
                    client.Dispose();
                }
            }
        }
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

            _serialConnection = GbxCartSerialConnectionFactory.Create(_options);
            _serialConnection.Open();
            await InitializeFirmwareInfoAsync(cancellationToken).ConfigureAwait(false);
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
            if (_serialConnection is null)
            {
                return;
            }

            try
            {
                await PowerOffAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
            }

            try
            {
                _serialConnection.Close();
            }
            finally
            {
                _serialConnection.Dispose();
                _serialConnection = null;
                _dmgReady = false;
            }
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<GbxCartCartridgeInfo> ReadCartridgeInfoAsync(CancellationToken cancellationToken = default)
    {
        await EnsureDmgReadyAsync(cancellationToken).ConfigureAwait(false);

        byte[] titleBytes = new byte[GbxCartProtocolConstants.TitleLength];
        for (int index = 0; index < titleBytes.Length; index++)
        {
            titleBytes[index] = await ReadRomByteAsync(GbxCartProtocolConstants.TitleAddress + index, cancellationToken).ConfigureAwait(false);
        }

        byte cartridgeType = await ReadRomByteAsync(0x0147, cancellationToken).ConfigureAwait(false);
        byte romSizeCode = await ReadRomByteAsync(0x0148, cancellationToken).ConfigureAwait(false);
        byte ramSizeCode = await ReadRomByteAsync(0x0149, cancellationToken).ConfigureAwait(false);
        string title = System.Text.Encoding.ASCII.GetString(titleBytes);

        return GbxCartCartridgeInfo.FromFields(title, cartridgeType, romSizeCode, ramSizeCode);
    }

    public async Task<byte[]> ReadHeaderAsync(CancellationToken cancellationToken = default)
    {
        await EnsureDmgReadyAsync(cancellationToken).ConfigureAwait(false);
        return await ReadRomWindowAsync(GbxCartProtocolConstants.HeaderAddress, GbxCartProtocolConstants.HeaderLength, cancellationToken).ConfigureAwait(false);
    }

    public async Task<GbxCartDumpResult> DumpGameBoyCameraAsync(GbxCartDumpMode mode, CancellationToken cancellationToken = default)
    {
        GbxCartCartridgeInfo info = await ReadCartridgeInfoAsync(cancellationToken).ConfigureAwait(false);

        byte[]? saveData = mode is GbxCartDumpMode.Save or GbxCartDumpMode.SaveAndRom
            ? await ReadGameBoyCameraSaveAsync(cancellationToken: cancellationToken).ConfigureAwait(false)
            : null;
        byte[]? romData = mode is GbxCartDumpMode.Rom or GbxCartDumpMode.SaveAndRom
            ? await ReadGameBoyCameraRomAsync(info.RomSizeBytes, cancellationToken: cancellationToken).ConfigureAwait(false)
            : null;

        return new GbxCartDumpResult(info, saveData, romData);
    }

    public async Task<byte[]> ReadGameBoyCameraSaveAsync(
        IProgress<GbxCartTransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        await PrepareGameBoyCameraSaveReadAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            progress?.Report(new GbxCartTransferProgress(
                "Save",
                0,
                GbxCartProtocolConstants.GameBoyCameraSaveBankCount,
                "Preparing Game Boy Camera save dump."));

            byte[] save = new byte[GbxCartProtocolConstants.GameBoyCameraSaveSizeBytes];
            for (int bank = 0; bank < GbxCartProtocolConstants.GameBoyCameraSaveBankCount; bank++)
            {
                byte[] bankBytes = await ReadGameBoyCameraSaveBankWithRetryAsync(bank, progress, cancellationToken).ConfigureAwait(false);
                Buffer.BlockCopy(bankBytes, 0, save, bank * GbxCartProtocolConstants.GameBoyCameraSaveBankSize, bankBytes.Length);
                progress?.Report(new GbxCartTransferProgress(
                    "Save",
                    bank + 1,
                    GbxCartProtocolConstants.GameBoyCameraSaveBankCount,
                    $"Read save bank {bank + 1}/{GbxCartProtocolConstants.GameBoyCameraSaveBankCount}."));
            }

            return save;
        }
        finally
        {
            await WriteRomRegisterAsync(0x0000, 0x00, CancellationToken.None).ConfigureAwait(false);
            await WriteRomRegisterAsync(0x6000, 0x00, CancellationToken.None).ConfigureAwait(false);
        }
    }

    public async Task<byte[]> ReadGameBoyCameraPhotoAlbumAsync(
        IProgress<GbxCartTransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        byte[] album = new byte[GbxCartProtocolConstants.GameBoyCameraPhotoAlbumSizeBytes];
        byte[] save = await ReadGameBoyCameraSaveAsync(progress, cancellationToken).ConfigureAwait(false);
        Buffer.BlockCopy(save, 0, album, 0, save.Length);

        int totalSteps = GbxCartProtocolConstants.GameBoyCameraSaveBankCount + GbxCartProtocolConstants.GameBoyCameraPhotoAlbumRomBankCount;
        progress?.Report(new GbxCartTransferProgress(
            "PhotoAlbum",
            GbxCartProtocolConstants.GameBoyCameraSaveBankCount,
            totalSteps,
            "Read Game Boy Camera save banks."));

        for (int bank = GbxCartProtocolConstants.GameBoyCameraPhotoAlbumFirstRomBank; bank < GbxCartProtocolConstants.GameBoyCameraRomBankCount; bank++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await WriteRomRegisterAsync(0x2000, bank & 0xFF, cancellationToken).ConfigureAwait(false);
            byte[] bankBytes = await ReadRomWindowAsync(0x4000, GbxCartProtocolConstants.GameBoyCameraRomBankSize, cancellationToken).ConfigureAwait(false);
            int albumOffset = GbxCartProtocolConstants.GameBoyCameraSaveSizeBytes
                + ((bank - GbxCartProtocolConstants.GameBoyCameraPhotoAlbumFirstRomBank) * GbxCartProtocolConstants.GameBoyCameraRomBankSize);
            Buffer.BlockCopy(bankBytes, 0, album, albumOffset, bankBytes.Length);

            int completed = GbxCartProtocolConstants.GameBoyCameraSaveBankCount
                + bank - GbxCartProtocolConstants.GameBoyCameraPhotoAlbumFirstRomBank + 1;
            progress?.Report(new GbxCartTransferProgress(
                "PhotoAlbum",
                completed,
                totalSteps,
                $"Read photo album ROM bank {bank}/{GbxCartProtocolConstants.GameBoyCameraRomBankCount - 1}."));
        }

        return album;
    }

    public async Task<byte[]> ReadGameBoyCameraRomAsync(
        IProgress<GbxCartTransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        GbxCartCartridgeInfo info = await ReadCartridgeInfoAsync(cancellationToken).ConfigureAwait(false);
        return await ReadGameBoyCameraRomAsync(info.RomSizeBytes, progress, cancellationToken).ConfigureAwait(false);
    }

    private async Task<byte[]> ReadGameBoyCameraRomAsync(
        int romSizeBytes,
        IProgress<GbxCartTransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureDmgReadyAsync(cancellationToken).ConfigureAwait(false);

        if (romSizeBytes <= 0 || romSizeBytes % GbxCartProtocolConstants.GameBoyCameraRomBankSize != 0)
        {
            throw new InvalidOperationException($"Unsupported Game Boy Camera ROM size {romSizeBytes} bytes.");
        }

        int romBankCount = romSizeBytes / GbxCartProtocolConstants.GameBoyCameraRomBankSize;

        progress?.Report(new GbxCartTransferProgress(
            "Rom",
            0,
            romBankCount,
            "Preparing Game Boy Camera ROM dump."));

        byte[] rom = new byte[romSizeBytes];
        byte[] bank0 = await ReadRomWindowAsync(0x0000, GbxCartProtocolConstants.GameBoyCameraRomBankSize, cancellationToken).ConfigureAwait(false);
        Buffer.BlockCopy(bank0, 0, rom, 0, bank0.Length);
        progress?.Report(new GbxCartTransferProgress(
            "Rom",
            1,
            romBankCount,
            $"Read ROM bank 1/{romBankCount}."));

        for (int bank = 1; bank < romBankCount; bank++)
        {
            await WriteRomRegisterAsync(0x2000, bank & 0xFF, cancellationToken).ConfigureAwait(false);
            byte[] bankBytes = await ReadRomWindowAsync(0x4000, GbxCartProtocolConstants.GameBoyCameraRomBankSize, cancellationToken).ConfigureAwait(false);
            Buffer.BlockCopy(bankBytes, 0, rom, bank * GbxCartProtocolConstants.GameBoyCameraRomBankSize, bankBytes.Length);
            progress?.Report(new GbxCartTransferProgress(
                "Rom",
                bank + 1,
                romBankCount,
                $"Read ROM bank {bank + 1}/{romBankCount}."));
        }

        return rom;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            _serialConnection?.Dispose();
        }
        finally
        {
            _serialConnection = null;
            _operationLock.Dispose();
        }
    }

    private async Task EnsureDmgReadyAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureConnected();
            if (_dmgReady)
            {
                return;
            }

            await SendCommandAsync(DeviceCommands["SET_MODE_DMG"], cancellationToken, waitForAcknowledgement: _requiresAcknowledgements).ConfigureAwait(false);
            await SendCommandAsync(DeviceCommands["SET_VOLTAGE_5V"], cancellationToken, waitForAcknowledgement: _requiresAcknowledgements).ConfigureAwait(false);
            if (_requiresAcknowledgements)
            {
                await SendCommandAsync(GetPowerOnCommand(), cancellationToken, waitForAcknowledgement: true).ConfigureAwait(false);
                await Task.Delay(_options.PowerSettleDelayMs, cancellationToken).ConfigureAwait(false);
                DiscardInputBuffer();
                await SetVariableAsync("DMG_READ_METHOD", 1, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await SendCommandAsync(DeviceCommands["OFW_QUERY_CART_PWR"], cancellationToken).ConfigureAwait(false);
                byte[] powerState = await ReadExactAsync(1, cancellationToken).ConfigureAwait(false);
                if (powerState[0] == 0)
                {
                    await SendCommandAsync(DeviceCommands["OFW_CART_PWR_ON"], cancellationToken).ConfigureAwait(false);
                    await Task.Delay(_options.PowerSettleDelayMs, cancellationToken).ConfigureAwait(false);
                    DiscardInputBuffer();
                }

                await SetVariableAsync("DMG_READ_METHOD", 1, cancellationToken).ConfigureAwait(false);
                await SetVariableAsync("CART_MODE", 1, cancellationToken).ConfigureAwait(false);
            }

            _dmgReady = true;
        }
        finally
        {
            _operationLock.Release();
        }
    }

    private async Task<byte[]> ReadRomWindowAsync(int address, int length, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureConnected();
            return await ReadWindowCoreAsync(address, length, 1, true, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    private async Task<byte> ReadRomByteAsync(int address, CancellationToken cancellationToken)
    {
        byte[] buffer = await ReadRomWindowAsync(address, 1, cancellationToken).ConfigureAwait(false);
        return buffer[0];
    }

    private async Task<byte[]> ReadSramWindowAsync(int address, int length, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureConnected();
            return await ReadWindowCoreAsync(0xA000 + address, length, 3, true, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    private async Task<byte[]> ReadWindowCoreAsync(int address, int length, byte dmgAccessMode, bool setReadPulse, CancellationToken cancellationToken)
    {
        byte[] output = new byte[length];
        int offset = 0;

        int configuredTransferSize = 0;
        int configuredAddress = int.MinValue;

        while (offset < length)
        {
            int chunkLength = Math.Min(GetReadChunkSize(), length - offset);
            int chunkAddress = address + offset;

            if (chunkLength != configuredTransferSize)
            {
                await SetVariableAsync("TRANSFER_SIZE", chunkLength, cancellationToken).ConfigureAwait(false);
                configuredTransferSize = chunkLength;
            }

            if (chunkAddress != configuredAddress)
            {
                await SetVariableAsync("ADDRESS", chunkAddress, cancellationToken).ConfigureAwait(false);
                configuredAddress = chunkAddress;
                await SetVariableAsync("DMG_ACCESS_MODE", dmgAccessMode, cancellationToken).ConfigureAwait(false);
                if (setReadPulse)
                {
                    await SetVariableAsync("DMG_READ_CS_PULSE", 1, cancellationToken).ConfigureAwait(false);
                }
            }

            int contiguousChunkCount = Math.Min((length - offset) / chunkLength, (GetReadChunkSize() == chunkLength ? int.MaxValue : 1));
            if (contiguousChunkCount == 0)
            {
                contiguousChunkCount = 1;
            }

            for (int index = 0; index < contiguousChunkCount; index++)
            {
                byte[] chunk = await ReadChunkWithRetryAsync(
                    chunkAddress: address + offset,
                    chunkLength,
                    dmgAccessMode,
                    setReadPulse,
                    cancellationToken).ConfigureAwait(false);
                Buffer.BlockCopy(chunk, 0, output, offset, chunkLength);
                offset += chunkLength;
            }
        }

        if (setReadPulse)
        {
            await SetVariableAsync("DMG_READ_CS_PULSE", 0, cancellationToken).ConfigureAwait(false);
        }

        return output;
    }

    private async Task<byte[]> ReadChunkWithRetryAsync(
        int chunkAddress,
        int chunkLength,
        byte dmgAccessMode,
        bool setReadPulse,
        CancellationToken cancellationToken)
    {
        int maxAttempts = Math.Max(1, _options.ReadRetryCount + 1);

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                DiscardInputBuffer();
                await SendCommandAsync(DeviceCommands["DMG_CART_READ"], cancellationToken).ConfigureAwait(false);
                return await ReadExactAsync(chunkLength, cancellationToken).ConfigureAwait(false);
            }
            catch (TimeoutException) when (attempt < maxAttempts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                DiscardInputBuffer();

                if (_options.ReadRetryDelayMs > 0)
                {
                    await Task.Delay(_options.ReadRetryDelayMs, cancellationToken).ConfigureAwait(false);
                }

                await SetVariableAsync("TRANSFER_SIZE", chunkLength, cancellationToken).ConfigureAwait(false);
                await SetVariableAsync("ADDRESS", chunkAddress, cancellationToken).ConfigureAwait(false);
                await SetVariableAsync("DMG_ACCESS_MODE", dmgAccessMode, cancellationToken).ConfigureAwait(false);
                if (setReadPulse)
                {
                    await SetVariableAsync("DMG_READ_CS_PULSE", 1, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        throw new InvalidOperationException("Read retry loop exited unexpectedly.");
    }

    private async Task<byte[]> ReadGameBoyCameraSaveBankWithRetryAsync(
        int bank,
        IProgress<GbxCartTransferProgress>? progress,
        CancellationToken cancellationToken)
    {
        int maxAttempts = Math.Max(1, _options.SaveBankRetryCount + 1);

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await WriteRomRegisterAsync(0x4000, bank, cancellationToken).ConfigureAwait(false);
                return await ReadSramWindowAsync(0, GbxCartProtocolConstants.GameBoyCameraSaveBankSize, cancellationToken).ConfigureAwait(false);
            }
            catch (TimeoutException) when (attempt < maxAttempts)
            {
                progress?.Report(new GbxCartTransferProgress(
                    "Save",
                    bank,
                    GbxCartProtocolConstants.GameBoyCameraSaveBankCount,
                    $"Timeout while reading save bank {bank + 1}. Re-priming and retrying attempt {attempt + 1}/{maxAttempts}."));
                await PrepareGameBoyCameraSaveReadAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        throw new InvalidOperationException("Save bank retry loop exited unexpectedly.");
    }

    private async Task PrepareGameBoyCameraSaveReadAsync(CancellationToken cancellationToken)
    {
        await EnsureDmgReadyAsync(cancellationToken).ConfigureAwait(false);
        await WriteRomRegisterAsync(0x6000, 0x01, cancellationToken).ConfigureAwait(false);
        await WriteRomRegisterAsync(0x0000, 0x0A, cancellationToken).ConfigureAwait(false);
    }

    private async Task WriteRomRegisterAsync(int address, int value, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureConnected();
            byte[] buffer = new byte[6];
            buffer[0] = DeviceCommands["DMG_CART_WRITE"];
            WriteBigEndian(buffer, 1, address);
            buffer[5] = unchecked((byte)value);
            await WriteAsync(buffer, cancellationToken, waitForAcknowledgement: _requiresAcknowledgements).ConfigureAwait(false);
            if (_options.RegisterWriteDelayMs > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(_options.RegisterWriteDelayMs, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _operationLock.Release();
        }
    }

    private async Task SetVariableAsync(string variableName, int value, CancellationToken cancellationToken)
    {
        (int bitWidth, byte key) = DeviceVariables[variableName];
        byte byteWidth = bitWidth switch
        {
            8 => 1,
            16 => 2,
            32 => 4,
            _ => throw new InvalidOperationException($"Unsupported variable width '{bitWidth}'."),
        };

        byte[] buffer = new byte[10];
        buffer[0] = DeviceCommands["SET_VARIABLE"];
        buffer[1] = byteWidth;
        WriteBigEndian(buffer, 2, key);
        WriteBigEndian(buffer, 6, value);
        await WriteAsync(buffer, cancellationToken, waitForAcknowledgement: _requiresAcknowledgements).ConfigureAwait(false);
        if (_options.SetVariableDelayMs > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(_options.SetVariableDelayMs, cancellationToken).ConfigureAwait(false);
        }
    }

    private Task PowerOffAsync(CancellationToken cancellationToken)
        => SendCommandAsync(GetPowerOffCommand(), cancellationToken, waitForAcknowledgement: _requiresAcknowledgements);

    private Task SendCommandAsync(byte command, CancellationToken cancellationToken, bool waitForAcknowledgement = false)
        => WriteAsync(new[] { command }, cancellationToken, waitForAcknowledgement);

    private async Task WriteAsync(byte[] buffer, CancellationToken cancellationToken, bool waitForAcknowledgement = false)
    {
        EnsureConnected();
        cancellationToken.ThrowIfCancellationRequested();
        _serialConnection!.Write(buffer, 0, buffer.Length);
        if (waitForAcknowledgement)
        {
            await WaitForAcknowledgementAsync(cancellationToken).ConfigureAwait(false);
        }
        if (_options.InterCommandDelayMs > 0)
        {
            await Task.Delay(_options.InterCommandDelayMs, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task InitializeFirmwareInfoAsync(CancellationToken cancellationToken)
    {
        DiscardInputBuffer();
        _requiresAcknowledgements = false;
        _firmwareVersion = 0;

        _serialConnection!.Write(new[] { DeviceCommands["QUERY_FW_INFO"] }, 0, 1);
        await Task.Delay(50, cancellationToken).ConfigureAwait(false);
        byte[] response = ReadAvailableBytes();

        if (response.Length >= 4)
        {
            _firmwareVersion = (response[2] << 8) | response[3];
            _requiresAcknowledgements = _firmwareVersion >= 12;
        }

        DiscardInputBuffer();
    }

    private async Task WaitForAcknowledgementAsync(CancellationToken cancellationToken)
    {
        byte[] acknowledgement = await ReadExactAsync(1, cancellationToken).ConfigureAwait(false);
        if (acknowledgement[0] != 0x01 && acknowledgement[0] != 0x03)
        {
            throw new InvalidOperationException($"Unexpected GBxCart acknowledgement byte 0x{acknowledgement[0]:X2} from {PortName}.");
        }
    }

    private byte[] ReadAvailableBytes()
    {
        EnsureConnected();

        return _serialConnection!.ReadAvailableBytes();
    }

    private byte GetPowerOnCommand() => _requiresAcknowledgements ? DeviceCommands["CART_PWR_ON"] : DeviceCommands["OFW_CART_PWR_ON"];

    private byte GetPowerOffCommand() => _requiresAcknowledgements ? DeviceCommands["CART_PWR_OFF"] : DeviceCommands["OFW_CART_PWR_OFF"];

    private int GetReadChunkSize() => _requiresAcknowledgements ? GbxCartProtocolConstants.LatestFirmwareTransferSize : GbxCartProtocolConstants.LegacyTransferSize;

    private Task<byte[]> ReadExactAsync(int count, CancellationToken cancellationToken)
    {
        EnsureConnected();
        return ReadExactCoreAsync(count, cancellationToken);
    }

    private async Task<byte[]> ReadExactCoreAsync(int count, CancellationToken cancellationToken)
    {
        try
        {
            return await _serialConnection!
                .ReadExactAsync(count, _options.CommandTimeoutMs, _options.PortReadTimeoutMs, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (TimeoutException ex)
        {
            throw new TimeoutException($"Timed out while waiting for {count} byte(s) from {PortName}.", ex);
        }
    }

    private void DiscardInputBuffer()
    {
        _serialConnection?.DiscardInBuffer();
    }

    private static void WriteBigEndian(byte[] destination, int offset, int value)
    {
        destination[offset] = unchecked((byte)((value >> 24) & 0xFF));
        destination[offset + 1] = unchecked((byte)((value >> 16) & 0xFF));
        destination[offset + 2] = unchecked((byte)((value >> 8) & 0xFF));
        destination[offset + 3] = unchecked((byte)(value & 0xFF));
    }

    private void EnsureConnected()
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("The GBxCart serial client is not connected.");
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(GbxCartSerialClient));
        }
    }
}

public sealed record GbxCartProbeResult(string PortName, GbxCartCartridgeInfo CartridgeInfo);
