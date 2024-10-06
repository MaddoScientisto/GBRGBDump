using System.Configuration;
using System.Data;
using System.Windows;
using GBRGBDump.GUI.Services;
using GBRGBDump.GUI.Services.Impl;
using GBTools.Bootstrapper;
using GBTools.Common;
using GBTools.Common.Services;
using GBTools.Decoder;
using GBTools.Graphics;
using Microsoft.Extensions.DependencyInjection;

namespace GBRGBDump.GUI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private ServiceProvider _serviceProvider;

        public App()
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();
        }

        private void ConfigureServices(ServiceCollection services)
        {
            services.AddTransient<ImageTransformService>();
            services.AddTransient<IImportSavService, ImportSavService>();
            services.AddTransient<IFileReaderService, FileReaderService>();
            services.AddTransient<IFileWriterService, FileWriterService>();

            services.AddTransient<IApplyFrameService, ApplyFrameService>();
            services.AddTransient<ICharMapService, CharMapService>();
            services.AddTransient<ICompressAndHashService, CompressAndHashService>();
            services.AddTransient<IFileMetaService, FileMetaService>();
            services.AddTransient<ITransformImageService, TransformImageService>();
            services.AddTransient<IParseCustomMetadataService, ParseCustomMetadataService>();
            services.AddTransient<IRandomIdService, RandomIdService>();
            services.AddTransient<IImportSavService, ImportSavService>();
            services.AddTransient<IFrameDataService, FrameDataService>();

            services.AddTransient<IDecoderService, DecoderService>();
            services.AddTransient<IGameboyPrinterService, GameboyPrinterService>();

            services.AddTransient<IRgbImageProcessingService, RgbImageProcessingService>();

            services.AddTransient<IDialogService, DialogService>();

            services.AddTransient<IFileSystemService, FileSystemService>();
            services.AddSingleton<ISettingsService, SettingsService>();

            services.AddTransient<MainViewModel>();

            services.AddTransient<RunScriptWindow>();
            services.AddTransient<RunScriptViewmodel>();


            services.AddSingleton<MainWindow>();
        }

        private void OnStartup(object sender, StartupEventArgs e)
        {
            var mainWindow = _serviceProvider.GetService<MainWindow>();
            mainWindow.DataContext = _serviceProvider.GetService<MainViewModel>();
            mainWindow.Show();
        }
    }

}
