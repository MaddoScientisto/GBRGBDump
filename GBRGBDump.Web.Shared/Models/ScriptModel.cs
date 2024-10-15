using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace GBRGBDump.Web.Shared.Models
{
    public class ScriptModel
    {
        public bool Enabled { get; set; } = false;
        public bool FailOnError { get; set; } = true;
        public string Path { get; set; }
        public string RunLocation { get; set; }
        public string Arguments { get; set; }

        [JsonIgnore]
        public bool Success { get; set; } = false;

        [JsonIgnore]
        public string Output { get; set; }
    }
}
