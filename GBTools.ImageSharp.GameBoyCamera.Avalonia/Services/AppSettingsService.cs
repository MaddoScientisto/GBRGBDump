using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace GBTools.ImageSharp.GameBoyCamera.Avalonia.Services;

public sealed class AppSettingsService : IAppSettingsService
{
    private readonly ILogger<AppSettingsService> _logger;
    private readonly string _settingsPath;
    private AppSettingsState _state;

    public AppSettingsService(ILogger<AppSettingsService> logger)
    {
        _logger = logger;
        _settingsPath = CreateSettingsPath();
        _state = Load();
    }

    public string? LastVideoExportPath
    {
        get => _state.LastVideoExportPath;
        set => _state.LastVideoExportPath = value;
    }

    public string? LastGbxCartPortName
    {
        get => _state.LastGbxCartPortName;
        set => _state.LastGbxCartPortName = value;
    }

    public string? LastGbxCartMode
    {
        get => _state.LastGbxCartMode;
        set => _state.LastGbxCartMode = value;
    }

    public string? LastPicoGbPrinterPortName
    {
        get => _state.LastPicoGbPrinterPortName;
        set => _state.LastPicoGbPrinterPortName = value;
    }

    public string? LastPicoGbPrinterMode
    {
        get => _state.LastPicoGbPrinterMode;
        set => _state.LastPicoGbPrinterMode = value;
    }

    public bool IgnoreDeletedPhotosForGbxCart
    {
        get => _state.IgnoreDeletedPhotosForGbxCart;
        set => _state.IgnoreDeletedPhotosForGbxCart = value;
    }

    public bool IgnoreLastSeenPhotoForGbxCart
    {
        get => _state.IgnoreLastSeenPhotoForGbxCart;
        set => _state.IgnoreLastSeenPhotoForGbxCart = value;
    }

    public bool AcceptBadDumpsForGbxCart
    {
        get => _state.AcceptBadDumpsForGbxCart;
        set => _state.AcceptBadDumpsForGbxCart = value;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath) ?? AppContext.BaseDirectory);
            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(_state, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception error)
        {
            _logger.LogWarning(error, "Failed to save Avalonia app settings to {Path}.", _settingsPath);
        }
    }

    private AppSettingsState Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return new AppSettingsState();
            }

            return JsonSerializer.Deserialize<AppSettingsState>(File.ReadAllText(_settingsPath)) ?? new AppSettingsState();
        }
        catch (Exception error)
        {
            _logger.LogWarning(error, "Failed to load Avalonia app settings from {Path}.", _settingsPath);
            return new AppSettingsState();
        }
    }

    private static string CreateSettingsPath()
    {
        string basePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(basePath))
        {
            basePath = AppContext.BaseDirectory;
        }

        return Path.Combine(basePath, "GBTools.ImageSharp.GameBoyCamera.Avalonia", "settings.json");
    }

    private sealed class AppSettingsState
    {
        public string? LastVideoExportPath { get; set; }

        public string? LastGbxCartPortName { get; set; }

        public string? LastGbxCartMode { get; set; } = GBTools.GBxCart.Serial.GbxCartDumpMode.Save.ToString();

        public string? LastPicoGbPrinterPortName { get; set; }

        public string? LastPicoGbPrinterMode { get; set; } = Models.PicoGbPrinterImportMode.WaitForNextCapture.ToString();

        public bool IgnoreDeletedPhotosForGbxCart { get; set; } = true;

        public bool IgnoreLastSeenPhotoForGbxCart { get; set; }

        public bool AcceptBadDumpsForGbxCart { get; set; }
    }
}