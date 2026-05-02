using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GBTools.ImageSharp.GameBoyCamera.Avalonia.Models;

namespace GBTools.ImageSharp.GameBoyCamera.Avalonia.ViewModels;

public sealed partial class PicNRecImportDialogViewModel : ViewModelBase
{
    [ObservableProperty]
    private PicNRecPortOption? selectedPort;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    public PicNRecImportDialogViewModel(
        IReadOnlyList<string> ports,
        PicNRecImportRequest? initialRequest,
        string? initialError)
    {
        AvailablePorts = CreatePortOptions(ports);
        SelectedPort = SelectInitialPort(AvailablePorts, initialRequest?.PortName);
        ErrorMessage = initialError ?? string.Empty;
        ConfirmCommand = new RelayCommand(Confirm);
        CancelCommand = new RelayCommand(Cancel);
    }

    public IReadOnlyList<PicNRecPortOption> AvailablePorts { get; }

    public string PortSummary => AvailablePorts.Count(static option => option.PortName is not null) switch
    {
        0 => "No serial ports were detected. Plug in the device and retry, or keep Automatic selected and try again later.",
        1 => "1 serial port was detected. Automatic selection will probe it, or you can choose it explicitly.",
        int count => $"{count} serial ports were detected. Pick one explicitly or use Automatic selection.",
    };

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public IRelayCommand ConfirmCommand { get; }

    public IRelayCommand CancelCommand { get; }

    public event Action<PicNRecImportRequest?>? CloseRequested;

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
        CloseRequested?.Invoke(new PicNRecImportRequest(SelectedPort?.PortName));
    }

    private void Cancel() => CloseRequested?.Invoke(null);

    private static IReadOnlyList<PicNRecPortOption> CreatePortOptions(IReadOnlyList<string> ports)
    {
        List<PicNRecPortOption> options =
        [
            new PicNRecPortOption(
                null,
                "Automatic selection",
                "Probe the detected serial ports and pick the first responsive PicNRec device.")
        ];

        options.AddRange(ports.Select(static portName => new PicNRecPortOption(
            portName,
            portName,
            $"Probe {portName} for the PicNRec metadata and image-read protocol.")));

        return options;
    }

    private static PicNRecPortOption SelectInitialPort(IReadOnlyList<PicNRecPortOption> ports, string? portName)
    {
        if (string.IsNullOrWhiteSpace(portName))
        {
            return ports[0];
        }

        return ports.FirstOrDefault(option => string.Equals(option.PortName, portName, StringComparison.OrdinalIgnoreCase)) ?? ports[0];
    }
}