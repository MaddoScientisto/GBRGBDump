using System.Diagnostics;
using System.Globalization;
using System.Text;
using GBTools.ImageSharp.GameBoyCamera.Codec;
using GBTools.ImageSharp.GameBoyCamera.Model;
using SixLabors.ImageSharp;

namespace GBTools.ImageSharp.GameBoyCamera.Video;

public sealed class FfmpegVideoExporter
{
    public const string DefaultTemplateFileName = "ffmpeg-video-args.txt";

    public async Task ExportAsync(
        IReadOnlyList<GbcPhoto> photos,
        FfmpegVideoExportOptions options,
        IProgress<string>? consoleOutput = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(photos);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.OutputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.FfmpegExecutable);

        if (photos.Count == 0)
        {
            throw new ArgumentException("At least one image is required to create a video.", nameof(photos));
        }

        if (options.Magnification < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options.Magnification), "Video magnification must be at least 1.");
        }

        if (options.FrameRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.FrameRate), "Video frame rate must be greater than zero.");
        }

        string templatePath = ResolveTemplatePath(options.TemplatePath);
        string template = await File.ReadAllTextAsync(templatePath, cancellationToken).ConfigureAwait(false);
        string tempDirectory = Path.Combine(Path.GetTempPath(), "gbc-video-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

        try
        {
            Directory.CreateDirectory(tempDirectory);
            await WriteFramesAsync(photos, tempDirectory, cancellationToken).ConfigureAwait(false);

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(options.OutputPath)) ?? Environment.CurrentDirectory);

            string inputPattern = Path.Combine(tempDirectory, "frame_%06d.png");
            string arguments = template
                .Replace("{FrameRate}", options.FrameRate.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("{InputPattern}", inputPattern, StringComparison.Ordinal)
                .Replace("{Magnification}", options.Magnification.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("{OutputPath}", options.OutputPath, StringComparison.Ordinal);

            await RunFfmpegAsync(options.FfmpegExecutable, SplitArguments(arguments), consoleOutput, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, recursive: true);
                }
            }
            catch
            {
            }
        }
    }

    public static string ResolveTemplatePath(string? templatePath = null)
    {
        if (!string.IsNullOrWhiteSpace(templatePath))
        {
            return Path.GetFullPath(templatePath);
        }

        string baseDirectoryPath = Path.Combine(AppContext.BaseDirectory, DefaultTemplateFileName);
        if (File.Exists(baseDirectoryPath))
        {
            return baseDirectoryPath;
        }

        string currentDirectoryPath = Path.Combine(Environment.CurrentDirectory, DefaultTemplateFileName);
        if (File.Exists(currentDirectoryPath))
        {
            return currentDirectoryPath;
        }

        throw new FileNotFoundException($"Could not find {DefaultTemplateFileName} next to the app or in the current directory.");
    }

    private static async Task WriteFramesAsync(IReadOnlyList<GbcPhoto> photos, string tempDirectory, CancellationToken cancellationToken)
    {
        for (int index = 0; index < photos.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string path = Path.Combine(tempDirectory, $"frame_{index:D6}.png");
            using var image = GameBoyCameraImageCodec.RenderPhoto(photos[index]);
            await image.SaveAsPngAsync(path, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task RunFfmpegAsync(
        string executable,
        IReadOnlyList<string> arguments,
        IProgress<string>? consoleOutput,
        CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo = new(executable)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        consoleOutput?.Report($"> {executable} {string.Join(" ", arguments)}");

        using Process process = new()
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true,
        };

        StringBuilder output = new();
        process.OutputDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is not null)
            {
                output.AppendLine(eventArgs.Data);
                consoleOutput?.Report(eventArgs.Data);
            }
        };
        process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is not null)
            {
                output.AppendLine(eventArgs.Data);
                consoleOutput?.Report(eventArgs.Data);
            }
        };

        try
        {
            process.Start();
        }
        catch (Exception error)
        {
            throw new InvalidOperationException($"Could not start ffmpeg executable '{executable}'. Make sure ffmpeg is available on PATH.", error);
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        consoleOutput?.Report($"ffmpeg exited with code {process.ExitCode}.");

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"ffmpeg exited with code {process.ExitCode}.{Environment.NewLine}{output}");
        }
    }

    private static IReadOnlyList<string> SplitArguments(string arguments)
    {
        List<string> result = [];
        StringBuilder current = new();
        bool inQuotes = false;

        for (int index = 0; index < arguments.Length; index++)
        {
            char character = arguments[index];
            if (character == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(character) && !inQuotes)
            {
                AddCurrentArgument(result, current);
                continue;
            }

            current.Append(character);
        }

        if (inQuotes)
        {
            throw new InvalidDataException("The ffmpeg argument template contains an unterminated quote.");
        }

        AddCurrentArgument(result, current);
        return result;
    }

    private static void AddCurrentArgument(List<string> arguments, StringBuilder current)
    {
        if (current.Length == 0)
        {
            return;
        }

        arguments.Add(current.ToString());
        current.Clear();
    }
}