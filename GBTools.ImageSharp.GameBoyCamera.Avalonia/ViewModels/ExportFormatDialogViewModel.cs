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
        ChooseGbBinBase64Command = new RelayCommand(ChooseGbBinBase64);
        ChooseJsonCommand = new RelayCommand(ChooseJson);
        ChooseTxtCommand = new RelayCommand(ChooseTxt);
        ChoosePngCommand = new RelayCommand(ChoosePng);
        CancelCommand = new RelayCommand(Cancel);
    }

    public IRelayCommand ChooseCanonicalCommand { get; }

    public IRelayCommand ChooseGbBinCommand { get; }

    public IRelayCommand ChooseGbBinBase64Command { get; }

    public IRelayCommand ChooseJsonCommand { get; }

    public IRelayCommand ChooseTxtCommand { get; }

    public IRelayCommand ChoosePngCommand { get; }

    public IRelayCommand CancelCommand { get; }

    public event Action<ExportRequest?>? CloseRequested;

    private void ChooseCanonical() => CloseRequested?.Invoke(new ExportRequest(ExportFormat.Canonical));

    private void ChooseGbBin() => CloseRequested?.Invoke(new ExportRequest(ExportFormat.GbBin));

    private void ChooseGbBinBase64() => CloseRequested?.Invoke(new ExportRequest(ExportFormat.GbBinBase64));

    private void ChooseJson() => CloseRequested?.Invoke(new ExportRequest(
        ExportFormat.GbPrinterWebJson,
        ExportSelectedToSingleFile: ExportSelectedToSingleFile));

    private void ChooseTxt() => CloseRequested?.Invoke(new ExportRequest(ExportFormat.Txt));

    private void ChoosePng() => CloseRequested?.Invoke(new ExportRequest(ExportFormat.Png));

    private void Cancel() => CloseRequested?.Invoke(null);
}