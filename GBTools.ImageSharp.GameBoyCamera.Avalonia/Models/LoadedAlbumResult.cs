using GBTools.ImageSharp.GameBoyCamera.Model;

namespace GBTools.ImageSharp.GameBoyCamera.Avalonia.Models;

public sealed record LoadedAlbumResult(
    GameBoyCameraSourceKind SourceKind,
    IReadOnlyList<LoadedPhotoInfo> Photos);

public sealed record LoadedPhotoInfo(
    string Title,
    string Created,
    GbcPhoto Photo,
    IReadOnlyList<MetadataEntry> MetadataEntries);

public sealed record MetadataEntry(string Label, string Value);

public sealed record PhotoExportRequest(string FileStem, string Title, string Created, GbcPhoto Photo);