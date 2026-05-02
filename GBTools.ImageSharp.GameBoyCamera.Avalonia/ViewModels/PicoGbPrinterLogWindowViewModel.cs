using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Threading;
using System.Text;

namespace GBTools.ImageSharp.GameBoyCamera.Avalonia.ViewModels;

public sealed partial class PicoGbPrinterLogWindowViewModel : ViewModelBase
{
    private readonly object _consoleLock = new();
    private readonly StringBuilder _pendingConsoleOutput = new();
    private bool _consoleFlushScheduled;

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
        lock (_consoleLock)
        {
            if (_pendingConsoleOutput.Length > 0 || !string.IsNullOrEmpty(ConsoleOutput))
            {
                _pendingConsoleOutput.AppendLine();
            }

            _pendingConsoleOutput.Append(line);

            if (_consoleFlushScheduled)
            {
                return;
            }

            _consoleFlushScheduled = true;
        }

        Dispatcher.UIThread.Post(FlushPendingConsoleOutput, DispatcherPriority.Background);
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
        lock (_consoleLock)
        {
            _pendingConsoleOutput.Clear();
            _consoleFlushScheduled = false;
        }

        ConsoleOutput = string.Empty;
    }

    private void FlushPendingConsoleOutput()
    {
        string pending;

        lock (_consoleLock)
        {
            pending = _pendingConsoleOutput.ToString();
            _pendingConsoleOutput.Clear();
            _consoleFlushScheduled = false;
        }

        if (string.IsNullOrEmpty(pending))
        {
            return;
        }

        ConsoleOutput += pending;
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