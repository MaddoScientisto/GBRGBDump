using Avalonia.Media.Imaging;
using GBTools.ImageSharp.GameBoyCamera.Model;

namespace GBTools.ImageSharp.GameBoyCamera.Avalonia.Services;

public interface IBitmapFactory
{
    Bitmap CreateThumbnailBitmap(GbcPhoto photo);

    Bitmap CreatePreviewBitmap(GbcPhoto photo);
}