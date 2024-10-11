using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GBRGBDump.Web.Shared.Models
{
    public class ScriptModel
    {
        public string Path { get; set; }
        public string RunLocation { get; set; }
        public string Arguments { get; set; }

        public bool Success { get; set; }

        public string Output { get; set; }
    }
}
