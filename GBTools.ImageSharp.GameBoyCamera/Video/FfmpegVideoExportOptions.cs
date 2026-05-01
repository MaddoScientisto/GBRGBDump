namespace GBTools.ImageSharp.GameBoyCamera.Video;

public sealed record FfmpegVideoExportOptions(
    string OutputPath,
    int Magnification,
    double FrameRate,
    string? TemplatePath = null,
    string FfmpegExecutable = "ffmpeg");