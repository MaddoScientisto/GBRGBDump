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
        => CreateBitmap(photo, maxWidth: 160, maxHeight: 144);

    public Bitmap CreatePreviewBitmap(GbcPhoto photo)
        => CreateBitmap(photo);

    private static Bitmap CreateBitmap(GbcPhoto photo, int? maxWidth = null, int? maxHeight = null)
    {
        ArgumentNullException.ThrowIfNull(photo);

        using Image<Rgba32> image = GameBoyCameraImageCodec.RenderPhoto(photo);
        if (maxWidth is int widthLimit && maxHeight is int heightLimit)
        {
            (int targetWidth, int targetHeight) = GetContainedSize(image.Width, image.Height, widthLimit, heightLimit);
            if (targetWidth != image.Width || targetHeight != image.Height)
            {
                image.Mutate(context => context.Resize(targetWidth, targetHeight, KnownResamplers.NearestNeighbor));
            }
        }

        using MemoryStream memoryStream = new();
        image.SaveAsPng(memoryStream);
        memoryStream.Position = 0;
        return new Bitmap(memoryStream);
    }

    private static (int Width, int Height) GetContainedSize(int sourceWidth, int sourceHeight, int maxWidth, int maxHeight)
    {
        if (sourceWidth <= maxWidth && sourceHeight <= maxHeight)
        {
            return (sourceWidth, sourceHeight);
        }

        double scale = Math.Min(maxWidth / (double)sourceWidth, maxHeight / (double)sourceHeight);
        return (
            Math.Max(1, (int)Math.Round(sourceWidth * scale)),
            Math.Max(1, (int)Math.Round(sourceHeight * scale)));
    }
}