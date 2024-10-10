using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GBRGBDump.Web.Shared.Models;
using Newtonsoft.Json;

namespace GBRGBDump.Web.Shared.Services.Impl
{
    public class LocalFileSystemJsonSettingsService : ISettingsService
    {
        public void SaveSettings(SettingsModel model)
        {
            var serialized = JsonConvert.SerializeObject(model);

            File.WriteAllText("settings.json", serialized);
        }

        public SettingsModel? LoadSettings()
        {
            if (!File.Exists("settings.json")) return null;

            var textData = File.ReadAllText("settings.json");

            return JsonConvert.DeserializeObject<SettingsModel>(textData);
        }
    }
}
