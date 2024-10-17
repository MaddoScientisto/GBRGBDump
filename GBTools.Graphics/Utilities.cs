using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GBTools.Graphics;

public static class Utilities
{
    public static string CanvasToBase64(Image<Rgba32> canvas)
    {
        using MemoryStream ms = new MemoryStream();
        canvas.Save(ms, new PngEncoder() { FilterMethod = PngFilterMethod.None });
        byte[] imageBytes = ms.ToArray();

        return Convert.ToBase64String(imageBytes);
    }
}

