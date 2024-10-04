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
        string OpenFileDialog(string? filter = null);
        string OpenFolderDialog(string? lastFolder = null);
    }


}
