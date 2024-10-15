using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GBRGBDump.Web.Shared.Services
{
    public  interface IFileSystemService
    {
        IEnumerable<string> GetFileSystemEntries(string path);
        Task<string> ImageToBase64Async(string imagePath);
        string MakeOutputSubFolder(string source, string destination);

        void CreateDirectory(string path);
    }
}
