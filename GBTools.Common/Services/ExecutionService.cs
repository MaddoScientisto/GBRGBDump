using System.Diagnostics;
using System.Text;

namespace GBTools.Common.Services;

public interface IExecutionService
{
    event EventHandler<string> OutputReceived;
    event EventHandler<string> ErrorReceived;

    Task<bool> RunScriptAsync(string scriptPath, string workingDirectory, string arguments);
    Task WriteToStandardInputAsync(string input);
}

public class ExecutionService : IExecutionService
{

    private Process _process;

    public event EventHandler<string> OutputReceived;
    public event EventHandler<string> ErrorReceived;

    public async Task<bool> RunScriptAsync(string scriptPath, string workingDirectory, string arguments)
    {
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = scriptPath, //"cmd.exe",
                Arguments = arguments, //$"/k \"{scriptPath} {arguments}\"",
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            _process = new Process() { StartInfo = processInfo };

            _process.OutputDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    outputBuilder.AppendLine(args.Data);
                    Debug.WriteLine($"STDOUT: {args.Data}");
                    OutputReceived?.Invoke(this, args.Data);
                }
            };

            _process.ErrorDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    errorBuilder.AppendLine(args.Data);
                    Debug.WriteLine($"STDERR: {args.Data}");
                    ErrorReceived?.Invoke(this, args.Data);
                }
            };


            _process.Start();

            _process.BeginOutputReadLine();   // Start asynchronously reading standard output
            _process.BeginErrorReadLine();    // Start asynchronously reading standard error
            await _process.WaitForExitAsync();

            // Capture the last line of output (or the entire output if desired)
            var lastOutputLine = outputBuilder.ToString().Trim().Split(Environment.NewLine).LastOrDefault();

            return _process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            var demystified = ex.Demystify();
            Debug.WriteLine(demystified);
            throw demystified;
        }
    }

    public async Task WriteToStandardInputAsync(string input)
    {
        if (_process?.StartInfo.RedirectStandardInput == true)
        {
            await _process.StandardInput.WriteLineAsync(input);
            await _process.StandardInput.FlushAsync();
        }
        else
        {
            throw new InvalidOperationException("The process was not started with input redirection enabled.");
        }
    }
}