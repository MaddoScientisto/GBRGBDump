using GBRGBDump.Web.Shared.Services;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GBRGBDump.Services.Impl
{
    public class FileDialogService : IFileDialogService
    {
        public string? OpenFileDialog(string? filter = null)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (!string.IsNullOrWhiteSpace(filter))
            {
                openFileDialog.Filter = filter;
            }

            if (openFileDialog.ShowDialog() == true)
            {
                return openFileDialog.FileName;
            }
            return null;
        }

        public string? OpenFolderDialog(string? lastFolder = null)
        {
            OpenFolderDialog openFolderDialog = new OpenFolderDialog();

            if (!string.IsNullOrWhiteSpace(lastFolder))
            {
                openFolderDialog.InitialDirectory = lastFolder;
            }

            if (openFolderDialog.ShowDialog() == true)
            {
                return openFolderDialog.FolderName;
            }

            return null;
        }
    }
}
