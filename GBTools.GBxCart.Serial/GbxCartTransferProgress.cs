namespace GBTools.GBxCart.Serial;

public sealed record GbxCartTransferProgress(
    string Operation,
    int CompletedUnits,
    int TotalUnits,
    string Message);