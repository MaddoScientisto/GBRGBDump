using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GBRGBDump.GUI
{
    public class Messenger
    {
        private static Messenger _instance;
        public static Messenger Default => _instance ??= new Messenger();

        private event Action<string> _showMessageRequested;

        public void RegisterShowMessage(Action<string> action)
        {
            _showMessageRequested += action;
        }

        public void SendShowMessage(string message)
        {
            _showMessageRequested?.Invoke(message);
        }
    }
}
