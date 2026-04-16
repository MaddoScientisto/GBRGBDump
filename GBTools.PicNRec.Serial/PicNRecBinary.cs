using System;

namespace GBTools.PicNRec.Serial;

public static class PicNRecBinary
{
    public static int DecodeLastImageNumber(byte[] metadata)
    {
        if (metadata == null)
        {
            throw new ArgumentNullException(nameof(metadata));
        }

        var lastImageNumber = 0;
        var length = Math.Min(metadata.Length, PicNRecProtocolConstants.LastImageScanBytes);

        for (var index = 0; index < length; index++)
        {
            switch (metadata[index])
            {
                case 0x00:
                    lastImageNumber += 8;
                    break;
                case 0x01:
                    lastImageNumber += 7;
                    break;
                case 0x03:
                    lastImageNumber += 6;
                    break;
                case 0x07:
                    lastImageNumber += 5;
                    break;
                case 0x0F:
                    lastImageNumber += 4;
                    break;
                case 0x1F:
                    lastImageNumber += 3;
                    break;
                case 0x3F:
                    lastImageNumber += 2;
                    break;
                case 0x7F:
                    lastImageNumber += 1;
                    break;
            }
        }

        return lastImageNumber;
    }

    public static byte[] CreateSavImageBuffer(byte[] imageBytes)
    {
        if (imageBytes == null)
        {
            throw new ArgumentNullException(nameof(imageBytes));
        }

        if (imageBytes.Length != PicNRecProtocolConstants.ImageSize)
        {
            throw new ArgumentException(
                $"Expected {PicNRecProtocolConstants.ImageSize} bytes, received {imageBytes.Length}.",
                nameof(imageBytes));
        }

        var copy = new byte[imageBytes.Length];
        Buffer.BlockCopy(imageBytes, 0, copy, 0, imageBytes.Length);
        return copy;
    }
}