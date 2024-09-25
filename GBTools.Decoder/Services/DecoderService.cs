using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GBTools.Decoder
{
    public interface IDecoderService
    {
        int[] Decode(string rawBytes);
    }

    public class DecoderService : IDecoderService
    {
        private const int TILE_PIXEL_WIDTH = 8; // Assuming a constant width, adjust as necessary
        private const int TILE_PIXEL_HEIGHT = 8; // Assuming a constant height, adjust as necessary

        public int[] Decode(string rawBytes)
        {
            string bytes = new string(rawBytes.Where(c => "0123456789ABCDEFabcdef".Contains(c)).ToArray());
            if (bytes.Length != 32) return null;

            byte[] byteArray = new byte[16];
            for (int i = 0; i < byteArray.Length; i++)
            {
                byteArray[i] = Convert.ToByte(bytes.Substring(i * 2, 2), 16);
            }

            int[] pixels = new int[TILE_PIXEL_WIDTH * TILE_PIXEL_HEIGHT];
            for (int j = 0; j < TILE_PIXEL_HEIGHT; j++)
            {
                for (int i = 0; i < TILE_PIXEL_WIDTH; i++)
                {
                    int hiBit = (byteArray[j * 2 + 1] >> (7 - i)) & 1;
                    int loBit = (byteArray[j * 2] >> (7 - i)) & 1;
                    pixels[j * TILE_PIXEL_WIDTH + i] = (hiBit << 1) | loBit;
                }
            }
            return pixels;
        }
    }
}
