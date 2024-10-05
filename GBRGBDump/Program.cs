using System.Diagnostics;
using GBRGBDump.Commands;
using GBRGBDump.Infrastructure;
using GBTools.Bootstrapper;
using GBTools.Common;
using GBTools.Decoder;
using GBTools.Graphics;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace GBRGBDump;
public class Program
{
    public static int Main(string[] args)
    {
        // Create a type registrar and register any dependencies.
        // A type registrar is an adapter for a DI framework.
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
        

        var registrar = new TypeRegistrar(serviceCollection);

        // Create a new command app with the registrar
        // and run it with the provided arguments.
        var app = new CommandApp<ProcessCommand>(registrar);
        return app.Run(args);
    }
}