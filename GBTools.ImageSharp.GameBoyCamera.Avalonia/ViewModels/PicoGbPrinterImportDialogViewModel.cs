using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GBTools.ImageSharp.GameBoyCamera.Avalonia.Models;

namespace GBTools.ImageSharp.GameBoyCamera.Avalonia.ViewModels;

public sealed partial class PicoGbPrinterImportDialogViewModel : ViewModelBase
{
    private static readonly IReadOnlyList<PicoGbPrinterImportMode> ImportModes =
    [
        PicoGbPrinterImportMode.WaitForNextCapture,
        PicoGbPrinterImportMode.LoadLastCapture,
    ];

    [ObservableProperty]
    private PicoGbPrinterPortOption? selectedPort;

    [ObservableProperty]
    private PicoGbPrinterImportMode selectedMode = PicoGbPrinterImportMode.WaitForNextCapture;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    public PicoGbPrinterImportDialogViewModel(
        IReadOnlyList<string> ports,
        PicoGbPrinterImportRequest? initialRequest,
        string? initialError)
    {
        AvailablePorts = CreatePortOptions(ports);
        SelectedPort = SelectInitialPort(AvailablePorts, initialRequest?.PortName);
        SelectedMode = initialRequest?.Mode ?? PicoGbPrinterImportMode.WaitForNextCapture;
        ErrorMessage = initialError ?? string.Empty;
        ConfirmCommand = new RelayCommand(Confirm);
        CancelCommand = new RelayCommand(Cancel);
    }

    public IReadOnlyList<PicoGbPrinterPortOption> AvailablePorts { get; }

    public IReadOnlyList<PicoGbPrinterImportMode> AvailableModes => ImportModes;

    public string PortSummary => AvailablePorts.Count(static option => option.PortName is not null) switch
    {
        0 => "No serial ports were detected. Plug in the Pico and retry, or keep Automatic selected and try again later.",
        1 => "1 serial port was detected. Automatic selection will probe it, or you can pick it explicitly.",
        int count => $"{count} serial ports were detected. Pick one explicitly or use Automatic selection.",
    };

    public string ModeSummary => SelectedMode == PicoGbPrinterImportMode.LoadLastCapture
        ? "Load the most recent capture already stored on the Pico. This returns immediately if a capture exists."
        : "Open the serial link and wait for the next live print capture from the Game Boy Printer session.";

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public IRelayCommand ConfirmCommand { get; }

    public IRelayCommand CancelCommand { get; }

    public event Action<PicoGbPrinterImportRequest?>? CloseRequested;

    partial void OnErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
    }

    partial void OnSelectedModeChanged(PicoGbPrinterImportMode value)
    {
        OnPropertyChanged(nameof(ModeSummary));
    }

    private void Confirm()
    {
        if (AvailablePorts.Count(static option => option.PortName is not null) == 0)
        {
            ErrorMessage = "No serial ports are available to probe yet.";
            return;
        }

        ErrorMessage = string.Empty;
        CloseRequested?.Invoke(new PicoGbPrinterImportRequest(SelectedPort?.PortName, SelectedMode));
    }

    private void Cancel() => CloseRequested?.Invoke(null);

    private static IReadOnlyList<PicoGbPrinterPortOption> CreatePortOptions(IReadOnlyList<string> ports)
    {
        List<PicoGbPrinterPortOption> options =
        [
            new PicoGbPrinterPortOption(
                null,
                "Automatic selection",
                "Probe the detected serial ports and pick the first responsive Pico GB Printer.")
        ];

        options.AddRange(ports.Select(static portName => new PicoGbPrinterPortOption(
            portName,
            portName,
            $"Probe {portName} for the Pico GB Printer startup banner and framed serial protocol.")));

        return options;
    }

    private static PicoGbPrinterPortOption SelectInitialPort(IReadOnlyList<PicoGbPrinterPortOption> ports, string? portName)
    {
        if (string.IsNullOrWhiteSpace(portName))
        {
            return ports[0];
        }

        return ports.FirstOrDefault(option => string.Equals(option.PortName, portName, StringComparison.OrdinalIgnoreCase)) ?? ports[0];
    }
}