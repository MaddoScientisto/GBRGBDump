using GBTools.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GBRGBDump.Web.Shared.Models
{
    public class SettingsModel
    {
        public string? InputPath { get; set; }
        public string? OutputPath { get; set; }

        public AverageTypes AverageType { get; set; } = AverageTypes.None;
        public ChannelOrder ChannelOrder { get; set; } = ChannelOrder.Sequential;

        public ScriptModel PreScript { get; set; } = new();

        public ScriptModel PostScript { get; set; } = new();
    }
}