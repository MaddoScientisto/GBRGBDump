using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GBRGBDump.GUI.Services
{
    public interface ISettingsService
    {
        string SourcePath { get; set; }
        string DestinationPath { get; set; }
        bool RememberSettings { get; set; }

        bool DoRGBMerge { get; set; }
        bool DoHDR { get; set; }

        void LoadSettings();
        void SaveSettings();
    }
}
