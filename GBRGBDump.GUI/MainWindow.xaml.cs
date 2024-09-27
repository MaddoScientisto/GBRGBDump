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

        private string _sourcePath;
        private string _destinationPath;

        private bool _rememberSettings;

        public MainWindow(ImageTransformService imageTransformService,
            IRgbImageProcessingService rgbImageProcessingService)
        {
            _imageTransformService = imageTransformService;
            _rgbImageProcessingService = rgbImageProcessingService;

            InitializeComponent();

            //LoadSettings();
        }

        private void LoadSettings()
        {
            if (!Properties.Settings.Default.RememberSettings)
                return;

            _sourcePath = Properties.Settings.Default.SourcePath;
            _destinationPath = Properties.Settings.Default.DestinationPath;
        }

        private void SaveSettings()
        {
            if (!_rememberSettings)
                return;

            Properties.Settings.Default.RememberSettings = _rememberSettings;
            Properties.Settings.Default.SourcePath = _sourcePath;
            Properties.Settings.Default.DestinationPath = _destinationPath;
            Properties.Settings.Default.Save();
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {

            if (string.IsNullOrWhiteSpace(_sourcePath))
            {
                // Show file picker and get the input filename
                OpenFileDialog openFileDialog = new OpenFileDialog();
                if (openFileDialog.ShowDialog() != true)
                {
                    return;
                }


                _sourcePath = openFileDialog.FileName;

                //UpdateLabels();
            }

            if (string.IsNullOrWhiteSpace(_destinationPath))
            {
                OpenFolderDialog openFolderDialog = new OpenFolderDialog();
                if (openFolderDialog.ShowDialog() != true)
                {
                    return;
                }

                _destinationPath = openFolderDialog.FolderName;
                //UpdateLabels();
            }


            string outputSubFolder = System.IO.Path.Combine(_destinationPath, System.IO.Path.GetFileNameWithoutExtension(_sourcePath));

            // Check if the input file exists
            if (!File.Exists(_sourcePath))
            {
                //Console.WriteLine($"The file {inputFilename} does not exist.");
                MessageBox.Show($"The file {_sourcePath} does not exist.");
                return;
            }

            // Check if the output directory exists, if not, create it
            if (!Directory.Exists(outputSubFolder))
            {
                Directory.CreateDirectory(outputSubFolder);
                Debug.WriteLine($"Created the directory: {outputSubFolder}");
            }

            await _imageTransformService.TransformSav(_sourcePath, outputSubFolder);
            Debug.WriteLine("Converted all the images, now merging...");


            _rgbImageProcessingService.ProcessImages(outputSubFolder, outputSubFolder, ChannelOrder.Sequential);

        }

        private void BtnPickSource_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() != true)
            {
                return;
            }


            _sourcePath = openFileDialog.FileName;
            //UpdateLabels();
        }

        //private void UpdateLabels()
        //{
        //    TxtSourcePath.Content = _sourcePath;
        //    TxtDestinationPath.Content = _destinationPath;
        //}

        private void BtnPickDestination_Click(object sender, RoutedEventArgs e)
        {
            OpenFolderDialog openFolderDialog = new OpenFolderDialog();
            if (openFolderDialog.ShowDialog() != true)
            {
                return;
            }

            _destinationPath = openFolderDialog.FolderName;

            //UpdateLabels();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            //SaveSettings();
        }
    }
}