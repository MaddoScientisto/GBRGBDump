using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GBTools.GBxCart.Serial;
using GBTools.ImageSharp.GameBoyCamera.Avalonia.Models;

namespace GBTools.ImageSharp.GameBoyCamera.Avalonia.ViewModels;

public sealed partial class GbxCartImportDialogViewModel : ViewModelBase
{
    private static readonly IReadOnlyList<GbxCartDumpMode> DumpModes =
    [
        GbxCartDumpMode.Save,
        GbxCartDumpMode.Rom,
        GbxCartDumpMode.SaveAndRom,
    ];

    [ObservableProperty]
    private GbxCartPortOption? selectedPort;

    [ObservableProperty]
    private GbxCartDumpMode selectedMode = GbxCartDumpMode.Save;

    [ObservableProperty]
    private bool ignoreDeletedPhotos = true;

    [ObservableProperty]
    private bool ignoreLastSeenPhoto;

    [ObservableProperty]
    private bool acceptBadDumps;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    public GbxCartImportDialogViewModel(
        IReadOnlyList<GbxCartPortInfo> ports,
        GbxCartImportRequest? initialRequest,
        string? initialError)
    {
        AvailablePorts = CreatePortOptions(ports);
        SelectedPort = SelectInitialPort(AvailablePorts, initialRequest?.PortName);
        SelectedMode = initialRequest?.Mode ?? GbxCartDumpMode.Save;
        IgnoreDeletedPhotos = initialRequest?.IgnoreDeletedPhotos ?? true;
        IgnoreLastSeenPhoto = initialRequest?.IgnoreLastSeenPhoto ?? false;
        AcceptBadDumps = initialRequest?.AcceptBadDumps ?? false;
        ErrorMessage = initialError ?? string.Empty;
        ConfirmCommand = new RelayCommand(Confirm);
        CancelCommand = new RelayCommand(Cancel);
    }

    public IReadOnlyList<GbxCartPortOption> AvailablePorts { get; }

    public IReadOnlyList<GbxCartDumpMode> AvailableModes => DumpModes;

    public string PortSummary => AvailablePorts.Count(static option => option.PortName is not null) switch
    {
        0 => "No serial ports were detected. Plug in the device and retry, or keep Automatic selected and try again later.",
        1 => "1 serial port was detected. Automatic selection will probe it, or you can choose it explicitly.",
        int count => $"{count} serial ports were detected. Pick one explicitly or use Automatic selection.",
    };

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public IRelayCommand ConfirmCommand { get; }

    public IRelayCommand CancelCommand { get; }

    public event Action<GbxCartImportRequest?>? CloseRequested;

    partial void OnErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
    }

    private void Confirm()
    {
        if (AvailablePorts.Count(static option => option.PortName is not null) == 0)
        {
            ErrorMessage = "No serial ports are available to probe yet.";
            return;
        }

        ErrorMessage = string.Empty;
        CloseRequested?.Invoke(new GbxCartImportRequest(
            SelectedPort?.PortName,
            SelectedMode,
            IgnoreDeletedPhotos,
            IgnoreLastSeenPhoto,
            AcceptBadDumps));
    }

    private void Cancel() => CloseRequested?.Invoke(null);

    private static IReadOnlyList<GbxCartPortOption> CreatePortOptions(IReadOnlyList<GbxCartPortInfo> ports)
    {
        List<GbxCartPortOption> options =
        [
            new GbxCartPortOption(
                null,
                "Automatic selection",
                "Probe the detected serial ports and pick the first responsive GBxCart-compatible device.")
        ];

        options.AddRange(ports.Select(static port => new GbxCartPortOption(
            port.PortName,
            $"{port.PortName} - {port.BestDescription}",
            BuildPortDetails(port))));

        return options;
    }

    private static GbxCartPortOption SelectInitialPort(IReadOnlyList<GbxCartPortOption> ports, string? portName)
    {
        if (string.IsNullOrWhiteSpace(portName))
        {
            return ports[0];
        }

        return ports.FirstOrDefault(option => string.Equals(option.PortName, portName, StringComparison.OrdinalIgnoreCase)) ?? ports[0];
    }

    private static string BuildPortDetails(GbxCartPortInfo port)
    {
        string guess = port.IsKnownUsbBridge
            ? "Likely GBxCart-compatible CH340 serial adapter"
            : "Unknown serial device";
        string vidPid = port.VendorId is int vendorId && port.ProductId is int productId
            ? $" VID:PID={vendorId:X4}:{productId:X4}"
            : string.Empty;
        string manufacturer = string.IsNullOrWhiteSpace(port.Manufacturer)
            ? string.Empty
            : $" Manufacturer: {port.Manufacturer}.";

        return $"{guess}.{manufacturer} {port.Description}{vidPid}".Trim();
    }
}