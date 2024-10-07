using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GBTools.Common;

namespace GBRGBDump.GUI.Services.Impl
{
    internal class SettingsService : ISettingsService
    {
        // public string SourcePath { get; set; }
        // public string DestinationPath { get; set; }
        // public bool RememberSettings { get; set; }
        // public bool DoRGBMerge { get; set; }
        // public bool DoHDR { get; set; }
        //
        // public bool RgbInterleaved { get; set; }
        //
        // public bool DoFullHDR { get; set; }

        public MainModel LoadSettings()
        {
            var model = new MainModel
            {
                RememberSettings = Properties.Settings.Default.RememberSettings
            };

            if (!model.RememberSettings) return model;

            model.SourcePath = Properties.Settings.Default.SourcePath;
            model.DestinationPath = Properties.Settings.Default.DestinationPath;

            model.AverageType = Enum.Parse<AverageTypes>(Properties.Settings.Default.AverageType);
            model.ChannelOrder = Enum.Parse<ChannelOrder>(Properties.Settings.Default.ChannelOrder);

            return model;
        }

        public void SaveSettings(MainModel settings)
        {
            Properties.Settings.Default.RememberSettings = settings.RememberSettings;

            if (!settings.RememberSettings)
            {
                Properties.Settings.Default.Save();
                return;
            }
            
            Properties.Settings.Default.SourcePath = settings.SourcePath;
            Properties.Settings.Default.DestinationPath = settings.DestinationPath;

            Properties.Settings.Default.AverageType = settings.AverageType.ToString();
            Properties.Settings.Default.ChannelOrder = settings.ChannelOrder.ToString();
            
            Properties.Settings.Default.Save();
        }
    }
}