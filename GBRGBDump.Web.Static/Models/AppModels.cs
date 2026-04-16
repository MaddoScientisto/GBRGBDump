using GBTools.ImageSharp.GameBoyCamera;
using GBTools.ImageSharp.GameBoyCamera.Model;

namespace GBRGBDump.Web.Static.Models;

public enum ExportFormat
{
    Json,
    Png,
    Gif,
    Jpeg,
    Bmp,
    GbBin,
    GbBinBase64,
    Gbci,
    Txt,
}

public sealed record ImportedPhoto(
    string SourceFileName,
    int PhotoIndex,
    string DisplayName,
    GameBoyCameraSourceKind SourceKind,
    GbcPhoto Photo,
    string PreviewDataUrl,
    int Width,
    int Height);

public sealed record ExportDownload(string FileName, string ContentType, byte[] Bytes);