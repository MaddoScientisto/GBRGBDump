﻿using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using GBRGBDump.Services.Impl;
using GBRGBDump.Web.Shared.Services;
using GBTools.Bootstrapper;
using GBTools.Common;
using GBTools.Decoder;
using GBTools.Graphics;
using Microsoft.Extensions.DependencyInjection;

namespace GBRGBDump.Web.GUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddWpfBlazorWebView();

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

            // Web services
            serviceCollection.AddTransient<IFileDialogService, FileDialogService>();

            Resources.Add("services", serviceCollection.BuildServiceProvider());
        }
    }
}