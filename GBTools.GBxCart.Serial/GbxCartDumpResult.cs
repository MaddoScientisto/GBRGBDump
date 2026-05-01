using System;

namespace GBTools.GBxCart.Serial;

public sealed record GbxCartDumpResult(
    GbxCartCartridgeInfo CartridgeInfo,
    byte[]? SaveData,
    byte[]? RomData);