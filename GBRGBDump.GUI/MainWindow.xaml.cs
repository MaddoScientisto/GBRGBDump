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
using GBRGBDump.GUI.Services;

namespace GBRGBDump.GUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ISettingsService _settingsService;

        public MainWindow(ISettingsService settingsService)
        {
            _settingsService = settingsService;

            InitializeComponent();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _settingsService.SaveSettings();
        }
    }
}