using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GBRGBDump.Web.Shared.Models;

namespace GBRGBDump.Web.Shared.Services
{
    public interface ISettingsService
    {
        void SaveSettings(SettingsModel model);
        SettingsModel? LoadSettings();
    }
}
