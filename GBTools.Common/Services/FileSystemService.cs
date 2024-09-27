using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GBTools.Common.Services
{
    public interface IFileSystemService
    {
        void CreateDirectory(string path);
        bool FileExists(string path);
    }

    public class FileSystemService : IFileSystemService
    {
        public void CreateDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        public bool FileExists(string path)
        {
            return File.Exists(path);
        }
    }
}