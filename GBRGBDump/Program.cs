using GBTools.Bootstrapper;
using GBTools.Common;
using GBTools.Decoder;
using GBTools.Graphics;
using Microsoft.Extensions.DependencyInjection;

if (args.Length < 2)
{
    Console.WriteLine("Please provide an input filename and an output folder.");
    return;
}

var serviceCollection = new ServiceCollection();

serviceCollection.AddTransient<ImageTransformService>();
serviceCollection.AddTransient<IImportSavService, ImportSavService>();
serviceCollection.AddTransient<IFileReaderService, FileReaderService>();
serviceCollection.AddTransient<IFileWriterService, FileWriterService>();

serviceCollection.AddTransient<IApplyFrameService, ApplyFrameService>();
serviceCollection.AddTransient<ICharMapService, CharMapService>();
serviceCollection.AddTransient<ICompressAndHashService, CompressAndHashService>();
serviceCollection.AddTransient<IFileMetaService, FileMetaService>();
serviceCollection.AddTransient<ITransformImageService, TransformImageService>();
serviceCollection.AddTransient<IParseCustomMetadataService, ParseCustomMetadataService>();
serviceCollection.AddTransient<IRandomIdService, RandomIdService>();
serviceCollection.AddTransient<IImportSavService, ImportSavService>();
serviceCollection.AddTransient<IFrameDataService, FrameDataService>();

serviceCollection.AddTransient<IDecoderService, DecoderService>();
serviceCollection.AddTransient<IGameboyPrinterService, GameboyPrinterService>();

serviceCollection.AddTransient<IRgbImageProcessingService, RgbImageProcessingService>();

var serviceProvider = serviceCollection.BuildServiceProvider();

var imageTransformService = serviceProvider.GetRequiredService<ImageTransformService>();

//await imageTransformService.TransformSav(@"C:\photodump-1.gbc", @"C:\temp\gbcdump");

string inputFilename = args[0];
string outputFolder = args[1];

string outputSubFolder = Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(inputFilename));

// Check if the input file exists
if (!File.Exists(inputFilename))
{
    Console.WriteLine($"The file {inputFilename} does not exist.");
    return;
}

// Check if the output directory exists, if not, create it
if (!Directory.Exists(outputSubFolder))
{
    Directory.CreateDirectory(outputSubFolder);
    Console.WriteLine($"Created the directory: {outputSubFolder}");
}

var progress = new Progress<ProgressInfo>(ReportProgress);

var result = await Task.Run(() => imageTransformService.TransformSav(inputFilename, outputSubFolder, progress));

//await imageTransformService.TransformSav(inputFilename, outputSubFolder);

Console.WriteLine("Converted all the images, now merging...");

var rgbImageProcessingService = serviceProvider.GetRequiredService<IRgbImageProcessingService>();

rgbImageProcessingService.ProcessImages(outputSubFolder, outputSubFolder, ChannelOrder.Sequential);
return;

void ReportProgress(ProgressInfo value)
{
    Console.WriteLine($"Bank: {value.CurrentBank}/{value.TotalBanks} Image: {value.CurrentImage}/{value.TotalImages} Name: {value.CurrentImageName}");
}