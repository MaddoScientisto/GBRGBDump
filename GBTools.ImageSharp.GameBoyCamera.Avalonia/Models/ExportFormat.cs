namespace GBTools.ImageSharp.GameBoyCamera.Avalonia.Models;

public enum ExportFormat
{
    Canonical,
    GbBin,
    GbPrinterWebJson,
    Png,
}

public static class ExportFormatExtensions
{
    public static string GetDisplayName(this ExportFormat format) => format switch
    {
        ExportFormat.Canonical => "Canonical Game Boy Camera (*.gbci)",
        ExportFormat.GbBin => "GB-BIN01 (*.bin)",
        ExportFormat.GbPrinterWebJson => "gb-printer-web JSON (*.json)",
        ExportFormat.Png => "PNG image (*.png)",
        _ => format.ToString(),
    };

    public static string GetDefaultExtension(this ExportFormat format) => format switch
    {
        ExportFormat.Canonical => "gbci",
        ExportFormat.GbBin => "bin",
        ExportFormat.GbPrinterWebJson => "json",
        ExportFormat.Png => "png",
        _ => "dat",
    };

    public static bool SupportsSingleFileAlbumExport(this ExportFormat format)
        => format == ExportFormat.GbPrinterWebJson;
}

public sealed record ExportRequest(
    ExportFormat Format,
    int PngMagnification = 1,
    bool ExportSelectedToSingleFile = false);