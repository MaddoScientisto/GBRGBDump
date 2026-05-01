namespace GBTools.ImageSharp.GameBoyCamera.Avalonia.Models;

public sealed record VideoExportRequest(string OutputPath, int Magnification, double FrameRate);