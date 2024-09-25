using GBRGBDump;
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

serviceCollection.AddTransient<ILocalStorageService, LocalStorageService>();

serviceCollection.AddTransient<IDecoderService, DecoderService>();
serviceCollection.AddTransient<IGameboyPrinterService, GameboyPrinterService>();

serviceCollection.AddSingleton<ILocalForage<FrameData>>(provider =>
    LocalForageFactory.CreateInstance<FrameData>("GB Printer Web", "gb-printer-web-frames"));

var serviceProvider = serviceCollection.BuildServiceProvider();

var imageTransformService = serviceProvider.GetRequiredService<ImageTransformService>();

//await imageTransformService.TransformSav(@"C:\photodump-1.gbc", @"C:\temp\gbcdump");

string inputFilename = args[0];
string outputFolder = args[1];

// Check if the input file exists
if (!File.Exists(inputFilename))
{
    Console.WriteLine($"The file {inputFilename} does not exist.");
    return;
}

// Check if the output directory exists, if not, create it
if (!Directory.Exists(outputFolder))
{
    Directory.CreateDirectory(outputFolder);
    Console.WriteLine($"Created the directory: {outputFolder}");
}

await imageTransformService.TransformSav(inputFilename, outputFolder);