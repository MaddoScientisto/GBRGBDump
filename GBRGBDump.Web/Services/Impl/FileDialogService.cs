using GBRGBDump.Web.Shared.Services;
using KristofferStrube.Blazor.FileSystem;
using KristofferStrube.Blazor.FileSystemAccess;
using System.Diagnostics;

namespace GBRGBDump.Web.Services.Impl
{
    public class FileDialogService : IFileDialogService
    {
        public string? OpenFileDialog(string? filter = null)
        {          
            return null;
        }

        public string? OpenFolderDialog(string? lastFolder = null)
        {
            return null;
        }
    }
}
