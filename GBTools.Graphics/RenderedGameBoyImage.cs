using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GBTools.Common;
using SkiaSharp;

namespace GBTools.Graphics
{
    public class RenderedGameBoyImage : IDisposable
    {
        public ImportItem RawData { get; set; }
        public SKImage? RenderedImage { get; set; }

        public void Dispose()
        {
            RenderedImage?.Dispose();
        }
    }
}
