using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GBRGBDump.GUI.Services
{
    public interface IDialogService
    {
        void ShowMessage(string message);
        void ShowError(Exception ex);
        string OpenFileDialog(string? filter = null);
        string OpenFolderDialog(string? lastFolder = null);
    }


}
