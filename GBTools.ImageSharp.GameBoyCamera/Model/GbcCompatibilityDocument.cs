namespace GBTools.ImageSharp.GameBoyCamera.Model;

public sealed record GbcCompatibilityDocument(
    GameBoyCameraSourceKind SourceKind,
    IReadOnlyDictionary<string, string> BinaryPayloads,
    IReadOnlyDictionary<string, object?> State);