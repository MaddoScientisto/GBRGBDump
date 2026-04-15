namespace GBTools.ImageSharp.GameBoyCamera.Metadata;

public sealed record GameBoyCameraCustomMetadata(
    GameBoyCameraRomType RomType,
    string Exposure,
    string CaptureMode,
    string? EdgeExclusive,
    string? EdgeOperation,
    string Gain,
    string EdgeMode,
    string InvertOutput,
    string VoltageReference,
    string ZeroPoint,
    string VoltageOutput,
    string? DitherSet,
    int? Contrast);