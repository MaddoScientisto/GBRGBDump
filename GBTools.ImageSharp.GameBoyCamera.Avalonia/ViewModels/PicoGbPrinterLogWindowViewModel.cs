using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace GBTools.ImageSharp.GameBoyCamera.Avalonia.ViewModels;

public sealed partial class PicoGbPrinterLogWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private string title;

    [ObservableProperty]
    private string consoleOutput = string.Empty;

    [ObservableProperty]
    private string statusText = "Waiting for Pico GB Printer activity...";

    [ObservableProperty]
    private bool isFinished;

    [ObservableProperty]
    private bool canStop = true;

    [ObservableProperty]
    private double progressValue;

    [ObservableProperty]
    private double progressMaximum = 1d;

    public PicoGbPrinterLogWindowViewModel(string title)
    {
        Title = title;
        CloseCommand = new RelayCommand(() => CloseRequested?.Invoke());
        ClearCommand = new RelayCommand(Clear);
        StopCommand = new RelayCommand(Stop, () => CanStop);
    }

    public IRelayCommand CloseCommand { get; }

    public IRelayCommand ClearCommand { get; }

    public IRelayCommand StopCommand { get; }

    public Exception? Error { get; private set; }

    public event Action? CloseRequested;

    public event Action? StopRequested;

    public void Reset(string title)
    {
        Title = title;
        ConsoleOutput = string.Empty;
        StatusText = "Waiting for Pico GB Printer activity...";
        Error = null;
        IsFinished = false;
        CanStop = true;
        ProgressValue = 0;
        ProgressMaximum = 1;
        StopCommand.NotifyCanExecuteChanged();
    }

    public void AppendLine(string line)
    {
        ConsoleOutput += string.IsNullOrEmpty(ConsoleOutput) ? line : Environment.NewLine + line;
    }

    public void SetStatus(string status)
    {
        StatusText = status;
    }

    public void SetProgress(double value, double maximum)
    {
        ProgressMaximum = Math.Max(1d, maximum);
        ProgressValue = Math.Clamp(value, 0d, ProgressMaximum);
    }

    public void MarkFinished(string status, Exception? error)
    {
        Error = error;
        StatusText = error is null ? status : $"Failed: {status}";
        IsFinished = true;
        CanStop = false;
        StopCommand.NotifyCanExecuteChanged();
    }

    public void MarkStopping()
    {
        if (!CanStop)
        {
            return;
        }

        CanStop = false;
        StatusText = "Stopping after the current capture frame completes...";
        StopCommand.NotifyCanExecuteChanged();
    }

    private void Clear()
    {
        ConsoleOutput = string.Empty;
    }

    private void Stop()
    {
        if (!CanStop)
        {
            return;
        }

        MarkStopping();
        StopRequested?.Invoke();
    }
}