using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GBRGBDump.GUI.Services.Impl
{
    internal class SettingsService : ISettingsService
    {
        public string SourcePath { get; set; }
        public string DestinationPath { get; set; }
        public bool RememberSettings { get; set; }
        public bool DoRGBMerge { get; set; }
        public bool DoHDR { get; set; }
        
        public bool RgbInterleaved { get; set; } 
        
        public bool DoFullHDR { get; set; }

        public void LoadSettings()
        {
            RememberSettings = Properties.Settings.Default.RememberSettings;

            if (!RememberSettings) return;

            SourcePath = Properties.Settings.Default.SourcePath;
            DestinationPath = Properties.Settings.Default.DestinationPath;

            DoRGBMerge = Properties.Settings.Default.RGBMerge;
            DoHDR = Properties.Settings.Default.HDR;
            DoFullHDR = Properties.Settings.Default.DoFullHDR;
            RgbInterleaved = Properties.Settings.Default.RGBInterleaved;
        }

        public void SaveSettings()
        {
            Properties.Settings.Default.RememberSettings = RememberSettings;

            if (!RememberSettings)
            {
                Properties.Settings.Default.Save();
                return;
            }
            
            Properties.Settings.Default.SourcePath = SourcePath;
            Properties.Settings.Default.DestinationPath = DestinationPath;
            Properties.Settings.Default.RGBMerge = DoRGBMerge;
            Properties.Settings.Default.HDR = DoHDR;
            Properties.Settings.Default.DoFullHDR = DoFullHDR;
            Properties.Settings.Default.RGBInterleaved = RgbInterleaved;

            Properties.Settings.Default.Save();
        }
    }
}