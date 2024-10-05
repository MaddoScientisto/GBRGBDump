using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace GBRGBDump.GUI.Services.Impl
{
    public class DialogService : IDialogService
    {
        public void ShowMessage(string message)
        {
            MessageBox.Show(message);
        }

        public void ShowError(Exception ex)
        {
            var dem = ex.Demystify();
            MessageBox.Show($"{dem.Message}\r\n{dem.StackTrace}");
        }

        public string OpenFileDialog(string? filter = null)
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

        public string OpenFolderDialog(string? lastFolder = null)
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
