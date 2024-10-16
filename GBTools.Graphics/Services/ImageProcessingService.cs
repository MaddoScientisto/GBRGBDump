using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Processing;
using GBTools.Common;

namespace GBTools.Graphics.Services;

public class ImageProcessingService
{
    public async Task<string> AverageAsync(List<string> base64Images)
    {
        if (base64Images == null || base64Images.Count == 0)
            throw new ArgumentException("List of base64 images cannot be empty.");

        // Decode the base64 images to Image objects
        var images = base64Images
            .Select(DecodeBase64Image)
            .ToList();

        int i = 1;
        foreach (var image in images.Skip(1))
        {
            float alpha = 1f / (i + 1); // Example: 1st image 1, 2nd image 0.5, 3rd image 0.33, etc.
            images[0].Mutate(ctx => ctx.DrawImage(image, PixelColorBlendingMode.Normal, alpha));
            i++;
        }

        var resultBase64Image = await Task.Run(() => EncodeImageToBase64(images[0]));

        foreach (var image in images)
        {
            image.Dispose();
        }

        return resultBase64Image;
    }

    public async Task<List<string>> RGBMergeAsync(List<string> base64Images, ChannelOrder channelOrder)
    {
        if (base64Images == null || base64Images.Count % 3 != 0)
            throw new ArgumentException("The list must contain images divisible by 3.");

        // Decode the base64 images to Image objects
        List<Image<L8>> grayscaleImages = base64Images
            .Select(DecodeBase64ImageL8)
            .ToList();

        int groupSize = grayscaleImages.Count / 3;

        var rgbImages = new List<Image<Rgba32>>();

        for (int i = 0; i < groupSize; i++)
        {
            Image<L8> redChannel, greenChannel, blueChannel;

            switch (channelOrder)
            {
                case ChannelOrder.Sequential:
                    redChannel = grayscaleImages[i];
                    greenChannel = grayscaleImages[i + groupSize];
                    blueChannel = grayscaleImages[i + (2 * groupSize)];
                    break;

                case ChannelOrder.Interleaved:
                    redChannel = grayscaleImages[3 * i];
                    greenChannel = grayscaleImages[3 * i + 1];
                    blueChannel = grayscaleImages[3 * i + 2];
                    break;

                default:
                    throw new ArgumentException("Unsupported channel order.");
            }

            // Create a new RGB image using the grayscale images as channels
            var rgbImage = new Image<Rgba32>(redChannel.Width, redChannel.Height);
            for (int y = 0; y < redChannel.Height; y++)
            {
                for (int x = 0; x < redChannel.Width; x++)
                {
                    byte r = redChannel[x, y].PackedValue;
                    byte g = greenChannel[x, y].PackedValue;
                    byte b = blueChannel[x, y].PackedValue;

                    rgbImage[x, y] = new Rgba32(r, g, b);
                }
            }

            rgbImages.Add(rgbImage);
        }

        var encodedImages = await Task.Run(() => rgbImages.Select(EncodeImageToBase64).ToList());

        foreach (var image in grayscaleImages)
        {
            image.Dispose();
        }

        foreach (var image in rgbImages)
        {
            image.Dispose();
        }

        return encodedImages;
    }

    // Helper method to decode a base64 string to an Image<Rgba32>
    private Image<Rgba32> DecodeBase64Image(string base64String)
    {
        byte[] imageBytes = Convert.FromBase64String(base64String);
        return Image.Load<Rgba32>(imageBytes);
    }

    // Helper method to decode a base64 string to an Image<L8> (grayscale)
    private Image<L8> DecodeBase64ImageL8(string base64String)
    {
        byte[] imageBytes = Convert.FromBase64String(base64String);
        return Image.Load<L8>(imageBytes);
    }

    // Helper method to encode an image to a base64 string
    private string EncodeImageToBase64(Image image)
    {
        using var memoryStream = new MemoryStream();
        image.SaveAsPng(memoryStream);
        byte[] imageBytes = memoryStream.ToArray();
        return Convert.ToBase64String(imageBytes);
    }
}