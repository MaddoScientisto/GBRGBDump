using System.Diagnostics;
using GBTools.Bootstrapper;
using GBTools.Graphics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;
using GBTools.Common;
using Microsoft.Extensions.DependencyInjection;
using System.IO;

namespace GBRGBDump.GUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ImageTransformService _imageTransformService;
        private readonly IRgbImageProcessingService _rgbImageProcessingService;

        public MainWindow(ImageTransformService imageTransformService,
            IRgbImageProcessingService rgbImageProcessingService)
        {
            _imageTransformService = imageTransformService;
            _rgbImageProcessingService = rgbImageProcessingService;

            InitializeComponent();
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            // Show file picker and get the input filename
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() != true)
            {
                return;
            }


            string inputFilename = openFileDialog.FileName;

            OpenFolderDialog openFolderDialog = new OpenFolderDialog();
            if (openFolderDialog.ShowDialog() != true)
            {
                return;
            }

            string outputFolder = openFolderDialog.FolderName;
            string outputSubFolder = System.IO.Path.Combine(outputFolder, System.IO.Path.GetFileNameWithoutExtension(inputFilename));

            // Check if the input file exists
            if (!File.Exists(inputFilename))
            {
                //Console.WriteLine($"The file {inputFilename} does not exist.");
                MessageBox.Show($"The file {inputFilename} does not exist.");
                return;
            }

            // Check if the output directory exists, if not, create it
            if (!Directory.Exists(outputSubFolder))
            {
                Directory.CreateDirectory(outputSubFolder);
                Debug.WriteLine($"Created the directory: {outputSubFolder}");
            }

            await _imageTransformService.TransformSav(inputFilename, outputSubFolder);
            Debug.WriteLine("Converted all the images, now merging...");


            _rgbImageProcessingService.ProcessImages(outputSubFolder, outputSubFolder, ChannelOrder.Sequential);

        }
    }
}