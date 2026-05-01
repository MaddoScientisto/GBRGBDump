using System;

namespace GBTools.GBxCart.Serial;

public sealed class GbxCartClientOptions
{
    public string PortName { get; init; } = string.Empty;

    public int BaudRate { get; init; } = GbxCartProtocolConstants.DefaultBaudRate;

    public int PortReadTimeoutMs { get; init; } = 500;

    public int PortWriteTimeoutMs { get; init; } = 1_000;

    public int CommandTimeoutMs { get; init; } = 2_000;

    public int PowerSettleDelayMs { get; init; } = 100;

    public int InterCommandDelayMs { get; init; }

    public int RegisterWriteDelayMs { get; init; } = 1;

    public int SetVariableDelayMs { get; init; } = 10;

    public int ReadRetryCount { get; init; } = 2;

    public int ReadRetryDelayMs { get; init; } = 1;

    public int SaveBankRetryCount { get; init; } = 2;
}