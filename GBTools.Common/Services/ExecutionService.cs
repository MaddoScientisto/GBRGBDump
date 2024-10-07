using System.Diagnostics;

namespace GBTools.Common.Services;

public interface IExecutionService
{
    Task<bool> RunScriptAsync(string scriptPath, string workingDirectory, string arguments);
}

public class ExecutionService : IExecutionService
{
    public async Task<bool> RunScriptAsync(string scriptPath, string workingDirectory, string arguments)
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = scriptPath, //"cmd.exe",
                Arguments = arguments, //$"/k \"{scriptPath} {arguments}\"",
                WorkingDirectory = workingDirectory,
                //RedirectStandardOutput = true,
                //RedirectStandardError = true,
                UseShellExecute = true,
                CreateNoWindow = false
            };

            using var process = new Process();
            process.StartInfo = processInfo;
            process.Start();

            // Read output if needed
            //var output = await process.StandardOutput.ReadToEndAsync();
            //var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            // You can log the output and errors if needed
            //Console.WriteLine("Output: " + output);
            //Console.WriteLine("Error: " + error);

            // Return true if the exit code is 0, otherwise return false
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            var demystified = ex.Demystify();
            Debug.WriteLine(demystified);
            throw demystified;
        }
    }
}