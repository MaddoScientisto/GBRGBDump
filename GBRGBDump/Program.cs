using GBRGBDump;
using Microsoft.Extensions.DependencyInjection;

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

serviceCollection.AddSingleton<ILocalForage<FrameData>>(provider =>
    LocalForageFactory.CreateInstance<FrameData>("GB Printer Web", "gb-printer-web-frames"));

var serviceProvider = serviceCollection.BuildServiceProvider();

var imageTransformService = serviceProvider.GetRequiredService<ImageTransformService>();

await imageTransformService.TransformSav(@"C:\photodump-1.gbc", @"C:\temp\gbcdump");