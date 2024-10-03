using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GBTools.Common
{
    public interface ITransformImageService
    {
        List<string> TransformImage(byte[] data, int baseAddress);
    }

    public class TransformImageService : ITransformImageService
    {
        private const string Black = "FF FF FF FF FF FF FF FF FF FF FF FF FF FF FF FF";
        private const string White = "00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00";

        public List<string> TransformImage(byte[] data, int baseAddress)
        {
            List<string> transformed = new List<string>();
            string currentLine = "";
            bool hasData = false;

            // Add black upper frame placeholder
            transformed.AddRange(Enumerable.Repeat(Black, 40));

            for (int i = 0; i < 0x0E00; i++)
            {
                if (i % 256 == 0)
                {
                    // Add left frame placeholder
                    transformed.AddRange(Enumerable.Repeat(Black, 2));
                }

                currentLine += $" {data[baseAddress + i].ToString("X2")}";

                if (i % 16 == 15)
                {
                    transformed.Add(currentLine.Trim());

                    // Track if an image has actual data inside to prevent importing the "white" image all the time
                    if (!hasData && currentLine.Trim() != White)
                    {
                        hasData = true;
                    }

                    currentLine = "";
                }

                if (i % 256 == 255)
                {
                    // Add right frame placeholder
                    transformed.AddRange(Enumerable.Repeat(Black, 2));
                }
            }

            // Add lower frame placeholder
            transformed.AddRange(Enumerable.Repeat(Black, 40));

            return hasData ? transformed : [];
        }
    }
}
