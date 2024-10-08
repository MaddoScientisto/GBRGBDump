using GBTools.Bootstrapper;
using GBTools.Common;
using GBTools.Decoder;
using GBTools.Graphics;
using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.Metrics;

namespace GBRGBDump.Web.MultiPlatformGUI;

public partial class Form1 : Form
{
    public Form1()
    {
        InitializeComponent();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddWindowsFormsBlazorWebView();

        serviceCollection.AddTransient<ImageTransformService>();
        serviceCollection.AddTransient<IImportSavService, ImportSavService>();
        serviceCollection.AddTransient<GBTools.Bootstrapper.IFileReaderService, FileReaderService>();
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

        blazorWebView1.HostPage = "wwwroot\\index.html";
        blazorWebView1.Services = serviceCollection.BuildServiceProvider();
        blazorWebView1.RootComponents.Add<MainView>("#app");
    }
}

