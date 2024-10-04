using System.ComponentModel;
using System.Diagnostics;
using GBTools.Bootstrapper;
using GBTools.Common;
using Spectre.Console.Cli;

namespace GBRGBDump.Commands;

public sealed class ProcessCommand : AsyncCommand<ProcessCommand.Settings>
{
    private readonly ImageTransformService _imageTransformService;

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0,"<INPUT_FILE_PATH>")]
        [Description("The path to the input file. Only .gbc and .sav files are supported.")]
        public string InputFilePath { get; set; }
        
        [CommandArgument(0,"<OUTPUT_PATH>")]
        [Description("The path to output the images to. A subfolder with the input file name will be created.")]
        public string OutputPath { get; set; }
        
        [CommandOption("-j|--cart-is-jp")]
        [Description("Process Japanese Game Boy Camera carts.")]
        public bool? CartIsJp { get; set; }
    }

    public ProcessCommand(ImageTransformService imageTransformService)
    {
        _imageTransformService = imageTransformService ?? throw new ArgumentNullException(nameof(imageTransformService));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        string outputSubFolder = Path.Combine(settings.OutputPath, Path.GetFileNameWithoutExtension(settings.OutputPath));

// Check if the input file exists
        if (!File.Exists(settings.InputFilePath))
        {
            Console.WriteLine($"The file {settings.InputFilePath} does not exist.");
            return 1; // TODO: Change to proper fail code
        }

// Check if the output directory exists, if not, create it
        if (!Directory.Exists(outputSubFolder))
        {
            Directory.CreateDirectory(outputSubFolder);
            Console.WriteLine($"Created the directory: {outputSubFolder}");
        }

        var progress = new Progress<ProgressInfo>(ReportProgress);

        Stopwatch s = new Stopwatch();
        s.Start();

        var importParams = new ImportSavOptions()
        {
            ImportLastSeen = false,
            ImportDeleted = true,
            ForceMagicCheck = false,
            AverageType = AverageTypes.FullBank,
            AebStep = 2,
            BanksToProcess = -1,
            CartIsJp = settings.CartIsJp ?? false,
        };


        var result = await Task.Run(() =>
            _imageTransformService.TransformSav(settings.InputFilePath, outputSubFolder, importParams, progress));
        
        s.Stop();
        Console.WriteLine($"Time Taken: {s.Elapsed:g}");
        
        return 0;
    }
    
    private void ReportProgress(ProgressInfo value)
    {
        Console.WriteLine(
            $"Bank: {value.CurrentBank}/{value.TotalBanks} Image: {value.CurrentImage}/{value.TotalImages} Name: {value.CurrentImageName}");
    }
}