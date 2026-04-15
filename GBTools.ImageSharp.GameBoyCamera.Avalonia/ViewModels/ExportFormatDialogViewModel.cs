using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GBTools.ImageSharp.GameBoyCamera.Avalonia.Models;

namespace GBTools.ImageSharp.GameBoyCamera.Avalonia.ViewModels;

public sealed partial class ExportFormatDialogViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool exportSelectedToSingleFile = true;

    public ExportFormatDialogViewModel()
    {
        ChooseCanonicalCommand = new RelayCommand(ChooseCanonical);
        ChooseGbBinCommand = new RelayCommand(ChooseGbBin);
        ChooseJsonCommand = new RelayCommand(ChooseJson);
        ChoosePngCommand = new RelayCommand(ChoosePng);
        CancelCommand = new RelayCommand(Cancel);
    }

    public IRelayCommand ChooseCanonicalCommand { get; }

    public IRelayCommand ChooseGbBinCommand { get; }

    public IRelayCommand ChooseJsonCommand { get; }

    public IRelayCommand ChoosePngCommand { get; }

    public IRelayCommand CancelCommand { get; }

    public event Action<ExportRequest?>? CloseRequested;

    private void ChooseCanonical() => CloseRequested?.Invoke(new ExportRequest(ExportFormat.Canonical));

    private void ChooseGbBin() => CloseRequested?.Invoke(new ExportRequest(ExportFormat.GbBin));

    private void ChooseJson() => CloseRequested?.Invoke(new ExportRequest(
        ExportFormat.GbPrinterWebJson,
        ExportSelectedToSingleFile: ExportSelectedToSingleFile));

    private void ChoosePng() => CloseRequested?.Invoke(new ExportRequest(ExportFormat.Png));

    private void Cancel() => CloseRequested?.Invoke(null);
}