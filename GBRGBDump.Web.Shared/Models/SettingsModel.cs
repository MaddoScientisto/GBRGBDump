using GBTools.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GBRGBDump.Web.Shared.Models;

public class Settings
{
    public SettingsModel Default { get; set; } = new SettingsModel();

    public IList<SettingsModel> Presets { get; set; } = [];


}

public class SettingsModel
{
    public string PresetName { get; set; } = "Default";

    public string? InputPath { get; set; }
    public string? OutputPath { get; set; }

    public AverageTypes AverageType { get; set; } = AverageTypes.None;
    public ChannelOrder ChannelOrder { get; set; } = ChannelOrder.Sequential;

    public ScriptModel PreScript { get; set; } = new();

    public ScriptModel PostScript { get; set; } = new();

    public string? PrinterAddress { get; set; } = "http://192.168.7.1";

    public int AebStep { get; set; } = 2;

    public int OutputSize { get; set; } = 1;
}