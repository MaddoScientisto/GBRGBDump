using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace GBTools.Graphics.Services;
public class PrinterImageService()
{
    const int COMMAND_INIT = 0x01;
    const int COMMAND_PRINT = 0x02;
    const int COMMAND_DATA = 0x04;
    const int COMMAND_TRANSFER = 0x10;

    const int PRINTER_WIDTH = 20;
    const int CAMERA_WIDTH = 16;
    const int TILE_SIZE = 0x10;
    const int TILE_HEIGHT = 8;
    const int TILE_WIDTH = 8;

    public async Task<List<string>> GetImages(byte[] resData)
    {
        var size = resData.Length;
        var processedData = new byte[(int)Math.Max(1024 * 1024, size)];

        using var canvas = new Image<Rgba32>(1, 1);

        var bufferStart = 0;
        var ptr = 0;
        var idx = 0;
        var len = 0;

        var canvases = new List<string>();

        await Task.Run(() =>
        {
            while (idx < size)
            {
                var command = resData[idx++];
                switch (command)
                {
                    case COMMAND_INIT:
                        break;
                    case COMMAND_PRINT:


                        if ((len = resData[idx++] | (resData[idx++] << 8)) != 4)
                        {
                            idx = size;
                            break;
                        }

                        var sheets = resData[idx++];
                        var margins = resData[idx++];
                        var palette = resData[idx++];
                        var exposure = (byte)Math.Min(0xFF, 0x80 + resData[idx++]);

                        palette = (palette != 0) ? palette : (byte)0xE4;

                        if (Render(canvas, processedData, bufferStart, ptr, PRINTER_WIDTH, sheets, margins, palette,
                                exposure))
                        {
                            canvases.Add(Utilities.CanvasToBase64(canvas));
                            canvas.Mutate(x => x.Resize(1, 1).Clear(SixLabors.ImageSharp.Color.Transparent));
                        }

                        bufferStart = ptr;

                        break;
                    case COMMAND_TRANSFER:
                        {
                            len = resData[idx++] | (resData[idx++] << 8);
                            var currentImageStart = ptr;

                            ptr = Decode(false, resData, size, len, idx, processedData, ptr);

                            idx += len;

                            Render(canvas, processedData, currentImageStart, ptr, CAMERA_WIDTH, 1, 0x03,
                                0xE4, 0Xff);

                            canvases.Add(Utilities.CanvasToBase64(canvas));
                            canvas.Mutate(x => x.Resize(1, 1).Clear(SixLabors.ImageSharp.Color.Transparent));

                            bufferStart = ptr;

                            break;
                        }
                    case COMMAND_DATA:
                        {
                            var compression = resData[idx++];
                            len = resData[idx++] | (resData[idx++] << 8);
                            ptr = Decode(compression != 0, resData, size, len, idx, processedData, ptr);
                            idx += len;
                            break;
                        }
                    default:
                        idx = size;
                        break;
                }

                if (canvas.Height > 1)
                {
                    canvases.Add(Utilities.CanvasToBase64(canvas));
                    canvas.Mutate(x => x.Resize(1, 1).Clear(SixLabors.ImageSharp.Color.Transparent));
                }
            }
        });

        return canvases;
    }

    private bool Render(Image<Rgba32> canvas, byte[] imageData, int imageStart, int imageEnd, int imageTileWidth, int sheets,
        byte margin, byte palette, byte exposure)
    {
        var canvasHeight = 1;
        var canvasWidth = 1;

        // Create a new palette (just like the Uint8Array in JavaScript)
        byte[] pal = new byte[4];
        pal[0] = (byte)((exposure * ((palette >> 0) & 0x03)) / 3);
        pal[1] = (byte)((exposure * ((palette >> 2) & 0x03)) / 3);
        pal[2] = (byte)((exposure * ((palette >> 4) & 0x03)) / 3);
        pal[3] = (byte)((exposure * ((palette >> 6) & 0x03)) / 3);

        // Determine initial tile positions
        int tile_y = (canvasHeight / TILE_HEIGHT);
        int tile_x = 0;

        // Resize canvas height dynamically
        int newCanvasHeight = ((canvasHeight >> 3) << 3) +
                              ((Math.Max(0, imageEnd - imageStart) / (TILE_SIZE * imageTileWidth)) >> 0) * 8;

        canvas.Mutate(x =>
            x
                .Resize(imageTileWidth * 8, newCanvasHeight)
                .Clear(SixLabors.ImageSharp.Color.Transparent)
        );

        // Iterate through the image data and modify the canvas pixels
        for (int i = imageStart; i < imageEnd;)
        {
            for (int t = 0; t < 8; t++)
            {
                byte b1 = imageData[i++];
                byte b2 = imageData[i++];

                for (int b = 0; b < 8; b++)
                {
                    // Calculate the pixel position
                    int posX = (tile_x * 8) + b;
                    int posY = (tile_y * 8) + t;

                    // Calculate the color index
                    int colorIndex = ((b1 >> (7 - b)) & 1) | (((b2 >> (7 - b)) & 1) << 1);

                    // Set the pixel color
                    byte grayscaleValue = (byte)(0xFF - pal[colorIndex]);
                    Rgba32 color = new Rgba32(grayscaleValue, grayscaleValue, grayscaleValue, 255); // Grayscale color
                    canvas[posX, posY] = color;
                }
            }

            // Move to the next tile
            tile_x += 1;
            if (tile_x >= imageTileWidth)
            {
                tile_x = 0;
                tile_y++;
            }
        }

        return ((margin & 0x0f) != 0);
    }

    private int Decode(
        bool isCompressed,
        byte[] sour,
        int sourSize,
        int sourDataLen,
        int sourPtr,
        byte[] dest,
        int destPtr)
    {
        // Check if the source pointer and data length are within the source size bounds
        if (sourPtr + sourDataLen <= sourSize)
        {
            if (isCompressed)
            {
                // Compressed data decoding
                int stop = sourPtr + sourDataLen;
                while (sourPtr < stop)
                {
                    byte tag = sour[sourPtr++];
                    if ((tag & 0x80) != 0) // Check if the most significant bit is set
                    {
                        // Read the compressed data
                        byte data = sour[sourPtr++];
                        for (int i = 0; i < ((tag & 0x7F) + 2); i++)
                        {
                            dest[destPtr++] = data;
                        }
                    }
                    else
                    {
                        // Copy uncompressed data
                        for (int i = 0; i < (tag + 1); i++)
                        {
                            dest[destPtr++] = sour[sourPtr++];
                        }
                    }
                }
            }
            else
            {
                // No compression, directly copy the source to destination
                for (int i = 0; i < sourDataLen; i++)
                {
                    dest[destPtr++] = sour[sourPtr++];
                }
            }
        }

        return destPtr;
    }
}