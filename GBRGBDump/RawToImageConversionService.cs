using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SkiaSharp;

namespace GBRGBDump
{
    public interface IGameboyPrinterService
    {
        void RenderAndSaveAsBmp(List<string> rawTiles, string filePath);
    }

    public class GameboyPrinterService : IGameboyPrinterService
    {
        private const int TilePixelWidth = 8;
        private const int TilePixelHeight = 8;
        private const int TilesPerLine = 20;

        public void RenderAndSaveAsBmp(List<string> rawTiles, string filePath)
        {
            var tiles = ParseAndDecode(rawTiles);
            using var bitmap = RenderTilesToBitmap(tiles);
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.OpenWrite(filePath);
            data.SaveTo(stream);
        }

        private List<int[]> ParseAndDecode(List<string> rawTiles)
        {
            
            //var lines = rawBytes.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var tiles = new List<int[]>();

            foreach (var line in rawTiles)
            {
                var tile = Decode(line);
                if (tile != null)
                    tiles.Add(tile);
            }

            return tiles;
        }

        private int[] Decode(string rawBytes)
        {
            var bytes = new string(rawBytes.Where(c => "0123456789ABCDEFabcdef".Contains(c)).ToArray());
            if (bytes.Length != 32) return null;

            var byteArray = new byte[16];
            for (int i = 0; i < byteArray.Length; i++)
            {
                byteArray[i] = Convert.ToByte(bytes.Substring(i * 2, 2), 16);
            }

            var pixels = new int[TilePixelWidth * TilePixelHeight];
            for (int j = 0; j < TilePixelHeight; j++)
            {
                for (int i = 0; i < TilePixelWidth; i++)
                {
                    int hiBit = (byteArray[j * 2 + 1] >> (7 - i)) & 1;
                    int loBit = (byteArray[j * 2] >> (7 - i)) & 1;
                    pixels[j * TilePixelWidth + i] = (hiBit << 1) | loBit;
                }
            }
            return pixels;
        }

        private SKBitmap RenderTilesToBitmap(List<int[]> tiles)
        {
            int tileHeightCount = tiles.Count / TilesPerLine;
            int imageWidth = TilePixelWidth * TilesPerLine;
            int imageHeight = TilePixelHeight * tileHeightCount;

            var bitmap = new SKBitmap(imageWidth, imageHeight);
            using (var canvas = new SKCanvas(bitmap))
            {
                canvas.Clear(SKColors.White); // Clear with white background

                for (int index = 0; index < tiles.Count; index++)
                {
                    var pixels = tiles[index];
                    int tileXOffset = index % TilesPerLine;
                    int tileYOffset = index / TilesPerLine;

                    for (int i = 0; i < TilePixelWidth; i++)
                    {
                        for (int j = 0; j < TilePixelHeight; j++)
                        {
                            var color = ColorFromPixelValue(pixels[j * TilePixelWidth + i]);
                            var paint = new SKPaint { Color = color };
                            canvas.DrawRect(tileXOffset * TilePixelWidth + i, tileYOffset * TilePixelHeight + j, 1, 1, paint);
                        }
                    }
                }
            }
            return bitmap;
        }

        private SKColor ColorFromPixelValue(int value)
        {
            // Simple grayscale palette
            switch (value)
            {
                case 0: return SKColors.White;
                case 1: return SKColor.Parse("#AAAAAA");
                case 2: return SKColor.Parse("#555555");
                default: return SKColors.Black;
            }
        }
    }
}
