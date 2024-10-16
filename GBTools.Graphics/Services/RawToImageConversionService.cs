﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GBTools.Common;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SkiaSharp;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Color = SixLabors.ImageSharp.Color;

namespace GBTools.Graphics
{
    public interface IGameboyPrinterService
    {
        Task RenderAndSaveAsPng(List<string> rawTiles, string filePath);
        Task<string> RenderAndSaveToBase64Png(List<string> rawTiles);
        SKImage RenderImage(List<string> rawTiles);
        Task SaveImageAsync(SKImage image, string filePath);
        Task RenderAndHDRMerge(List<ImportItem> rawItems, string outputPath, AverageTypes averageType, ChannelOrder channelOrder);
    }

    public class GameboyPrinterService : IGameboyPrinterService
    {
        private const int TilePixelWidth = 8;
        private const int TilePixelHeight = 8;
        private const int TilesPerLine = 20;

        private readonly IRgbImageProcessingService _rgbImageProcessingService;

        public GameboyPrinterService(IRgbImageProcessingService rgbImageProcessingService)
        {
            _rgbImageProcessingService = rgbImageProcessingService;
        }

        public async Task RenderAndSaveAsPng(List<string> rawTiles, string filePath)
        {
            using var image = RenderImage(rawTiles);
            await SaveImageAsync(image, filePath);
        }

        public async Task<string> RenderAndSaveToBase64Png(List<string> rawTiles)
        {
            return await Task.Run(() =>
            {
                var tiles = ParseAndDecode(rawTiles);

                using var image = RenderTilesToImage(tiles);

                var base64EncodedImage = Utilities.CanvasToBase64(image);

                return base64EncodedImage;
            });


        }

        public SKImage RenderImage(List<string> rawTiles)
        {
            var tiles = ParseAndDecode(rawTiles);



            using var bitmap = RenderTilesToBitmap(tiles);
            var image = SKImage.FromBitmap(bitmap);
            return image;
        }

        public async Task SaveImageAsync(SKImage image, string filePath)
        {
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            await using var stream = File.OpenWrite(filePath);
            data.SaveTo(stream);
            
        }

        public async Task RenderAndHDRMerge(List<ImportItem> rawItems, string outputPath, AverageTypes averageType, ChannelOrder channelOrder)
        {
            var renderedItems = RenderImages(rawItems);

            await _rgbImageProcessingService.ProcessImages(renderedItems, outputPath, channelOrder, averageType);
            
            foreach (var renderedGameBoyImage in renderedItems)
            {
                renderedGameBoyImage.RenderedImage?.Dispose();
            }

            // Do HDR
            // Save Images
            // Dispose

        }

        private List<RenderedGameBoyImage> RenderImages(List<ImportItem> rawItems)
        {
            var renderedItems = new List<RenderedGameBoyImage>();

            foreach (var importItem in rawItems)
            {
                var renderedItem = new RenderedGameBoyImage()
                {
                    RawData = importItem,
                    RenderedImage = RenderImage(importItem.Tiles)
                };
                renderedItems.Add(renderedItem);
            }

            return renderedItems;
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
            using var canvas = new SKCanvas(bitmap);
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
                        using var paint = new SKPaint();
                        paint.Color = color;
                        paint.IsAntialias = false;
                        paint.FilterQuality = SKFilterQuality.None;
                        canvas.DrawPoint(tileXOffset * TilePixelWidth + i, tileYOffset * TilePixelHeight + j, paint);
                        //canvas.DrawRect(tileXOffset * TilePixelWidth + i, tileYOffset * TilePixelHeight + j, 1, 1, paint);
                    }
                }
            }

            return bitmap;
        }

        private Image<Rgba32> RenderTilesToImage(List<int[]> tiles)
        {
            int tileHeightCount = tiles.Count / TilesPerLine;
            int imageWidth = TilePixelWidth * TilesPerLine;
            int imageHeight = TilePixelHeight * tileHeightCount;

            var bitmap = new Image<Rgba32>(imageWidth, imageHeight);
            bitmap.Mutate(x => x.Clear(Color.White));

            for (int index = 0; index < tiles.Count; index++)
            {
                var pixels = tiles[index];
                int tileXOffset = index % TilesPerLine;
                int tileYOffset = index / TilesPerLine;

                for (int i = 0; i < TilePixelWidth; i++)
                {
                    for (int j = 0; j < TilePixelHeight; j++)
                    {
                        var color = Palette[pixels[j * TilePixelWidth + i]];

                        bitmap[tileXOffset * TilePixelWidth + i, tileYOffset * TilePixelHeight + j] = color;
                    }
                }
            }

            return bitmap;
        }

        private readonly Dictionary<int, Rgba32> Palette = new Dictionary<int, Rgba32>()
        {
            {0, Color.White},
            {1, Color.ParseHex("#AAAAAA")},
            {2, Color.ParseHex("#555555")},
            {3, Color.Black}
        };

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
