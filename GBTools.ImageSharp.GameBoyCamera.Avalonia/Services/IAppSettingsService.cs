namespace GBTools.ImageSharp.GameBoyCamera.Avalonia.Services;

public interface IAppSettingsService
{
    string? LastVideoExportPath { get; set; }

    void Save();
}