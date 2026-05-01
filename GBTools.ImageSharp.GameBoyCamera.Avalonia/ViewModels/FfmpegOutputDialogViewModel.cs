using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace GBTools.ImageSharp.GameBoyCamera.Avalonia.ViewModels;

public sealed partial class FfmpegOutputDialogViewModel : ViewModelBase
{
    private readonly string _outputPath;

    [ObservableProperty]
    private string consoleOutput = string.Empty;

    [ObservableProperty]
    private bool isFinished;

    [ObservableProperty]
    private string statusText = "Running ffmpeg...";

    public FfmpegOutputDialogViewModel(string outputPath)
    {
        _outputPath = outputPath;
        OpenFolderCommand = new RelayCommand(OpenFolder, () => IsFinished);
        CloseCommand = new RelayCommand(() => CloseRequested?.Invoke(), () => IsFinished);
    }

    public IRelayCommand OpenFolderCommand { get; }

    public IRelayCommand CloseCommand { get; }

    public Exception? Error { get; private set; }

    public event Action? CloseRequested;

    public void AppendLine(string line)
    {
        ConsoleOutput += string.IsNullOrEmpty(ConsoleOutput) ? line : Environment.NewLine + line;
    }

    public void MarkFinished(Exception? error)
    {
        Error = error;
        StatusText = error is null ? "ffmpeg finished." : $"ffmpeg failed: {error.GetType().Name}: {error.Message}";
        IsFinished = true;
        OpenFolderCommand.NotifyCanExecuteChanged();
        CloseCommand.NotifyCanExecuteChanged();
    }

    private void OpenFolder()
    {
        string? folder = Path.GetDirectoryName(Path.GetFullPath(_outputPath));
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true,
            });
        }
        catch
        {
        }
    }
}