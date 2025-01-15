using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GBRGBDump.Web.Shared.Models;
using GBRGBDump.WebServer;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace GBRGBDump.Web.Shared.Services.Impl
{
    public class LocalFileSystemJsonSettingsService : ISettingsService
    {

        private readonly SettingsConfig _config;
        public LocalFileSystemJsonSettingsService(IOptions<SettingsConfig> config)
        {
            _config = config.Value;
        }

        public void SaveSettings(SettingsModel model)
        {
            var serialized = JsonConvert.SerializeObject(model);

            File.WriteAllText(_config.Location, serialized);
        }

        public SettingsModel? LoadSettings()
        {
            if (!File.Exists(_config.Location)) return null;

            var textData = File.ReadAllText(_config.Location);

            return JsonConvert.DeserializeObject<SettingsModel>(textData);
        }
    }
}
