using System.IO;
using Avalonia.Media.Imaging;
using GBTools.ImageSharp.GameBoyCamera.Codec;
using GBTools.ImageSharp.GameBoyCamera.Model;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace GBTools.ImageSharp.GameBoyCamera.Avalonia.Services;

public sealed class BitmapFactory : IBitmapFactory
{
    public Bitmap CreateThumbnailBitmap(GbcPhoto photo)
        => CreateBitmap(photo, width: 80, height: 72);

    public Bitmap CreatePreviewBitmap(GbcPhoto photo)
        => CreateBitmap(photo, width: 160, height: 144);

    private static Bitmap CreateBitmap(GbcPhoto photo, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(photo);

        using Image<Rgba32> image = GameBoyCameraTileGridRenderer.Render(photo.TileGrid);
        if (image.Width != width || image.Height != height)
        {
            image.Mutate(context => context.Resize(width, height, KnownResamplers.NearestNeighbor));
        }

        using MemoryStream memoryStream = new();
        image.SaveAsPng(memoryStream);
        memoryStream.Position = 0;
        return new Bitmap(memoryStream);
    }
}