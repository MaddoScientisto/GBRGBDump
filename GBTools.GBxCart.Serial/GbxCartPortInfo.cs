using System;

namespace GBTools.GBxCart.Serial;

public sealed record GbxCartPortInfo(
    string PortName,
    string FriendlyName,
    string Description,
    string? Manufacturer,
    string? PnpDeviceId,
    int? VendorId,
    int? ProductId)
{
    public bool IsKnownUsbBridge => VendorId == 0x1A86 && ProductId == 0x7523;

    public string BestDescription => !string.IsNullOrWhiteSpace(FriendlyName)
        ? FriendlyName
        : !string.IsNullOrWhiteSpace(Description)
            ? Description
            : PortName;
}