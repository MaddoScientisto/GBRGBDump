using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace GBTools.ImageSharp.GameBoyCamera.Lite;

/// <summary>
/// Decodes the raw `/download` payload produced by the Pico GB Printer
/// (https://github.com/untoxa/pico-gb-printer). The payload is a concatenation
/// of Game Boy printer command packets carrying tile data + print commands,
/// <em>not</em> the tile-only `GB-BIN01` format decoded by
/// <see cref="GameBoyCameraBinDecoder"/>.
/// </summary>
/// <remarks>
/// Mirrors the logic in the Pico frontend's <c>downloadDataToImageData.ts</c>
/// (see <c>pico-gb-printer/frontend/src/functions/decoding/</c>). Stream grammar:
/// <code>
/// stream := packet*
/// packet :=
///   0x01                                       // INIT, no payload
/// | 0x02 len:u16le(=4) sheets margins pal exp  // PRINT (flushes current strip)
/// | 0x04 compression:u8 len:u16le data[len]    // DATA (tiles, maybe RLE)
/// | 0x10 len:u16le data[len]                   // TRANSFER (raw tiles, no RLE)
/// </code>
/// RLE: for each byte <c>tag</c>,
/// <c>tag &amp; 0x80</c> = "repeat next byte <c>(tag &amp; 0x7f)+2</c> times",
/// otherwise "copy next <c>tag+1</c> bytes literal".
/// </remarks>
public static class PicoGbPrinterPacketDecoder
{
    private const byte CommandInit = 0x01;
    private const byte CommandPrint = 0x02;
    private const byte CommandData = 0x04;
    private const byte CommandTransfer = 0x10;

    private const int PrinterWidthTiles = 20;
    private const int CameraWidthTiles = 16;
    private const int TileByteCount = 16;
    private const int TilePixelSize = 8;

    private static readonly Rgba32[] DefaultPalette =
    [
        new(255, 255, 255, 255),
        new(170, 170, 170, 255),
        new(85, 85, 85, 255),
        new(0, 0, 0, 255),
    ];

    /// <summary>
    /// Detects whether the payload plausibly starts with a Pico GB Printer
    /// command packet. Used for format auto-selection.
    /// </summary>
    public static bool LooksLikePicoStream(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return false;
        }

        byte first = data[0];
        return first is CommandInit or CommandPrint or CommandData or CommandTransfer;
    }

    /// <summary>
    /// Decodes every image in the stream. Each <c>PRINT</c> command (or
    /// <c>TRANSFER</c> command with non-empty output) yields one image.
    /// </summary>
    /// <exception cref="InvalidDataException">Malformed packet stream.</exception>
    public static IReadOnlyList<Image<Rgba32>> DecodeAll(ReadOnlySpan<byte> data)
    {
        List<Image<Rgba32>> images = [];
        // Rolling tile buffer. One Game Boy printer strip is 20 tiles wide
        // and at most ~12 rows tall per print, so 1 MiB is plenty even for
        // long prints.
        byte[] tileBuffer = new byte[1024 * 1024];
        int stripStart = 0;
        int ptr = 0;
        int idx = 0;

        while (idx < data.Length)
        {
            byte command = data[idx++];
            switch (command)
            {
                case CommandInit:
                    // No payload.
                    break;

                case CommandPrint:
                    {
                        if (idx + 2 > data.Length)
                        {
                            // Truncated PRINT header: emit whatever tile data we have
                            // accumulated and stop, matching the on-device parser.
                            idx = data.Length;
                            break;
                        }

                        int len = data[idx++] | (data[idx++] << 8);
                        if (len != 4)
                        {
                            // Per the Pico frontend, a non-4 PRINT terminates the stream.
                            idx = data.Length;
                            break;
                        }

                        if (idx + 4 > data.Length)
                        {
                            idx = data.Length;
                            break;
                        }

                        // sheets, margins, palette, exposure. We honour palette
                        // via the default 4-shade ramp; `margins` upper nibble is
                        // "feed before", lower nibble is "feed after". On real
                        // hardware a non-zero "feed after" ends the current image.
                        idx++; // sheets
                        byte margins = data[idx++];
                        byte palette = data[idx++];
                        idx++; // exposure

                        Image<Rgba32>? image = FlushStrip(
                            tileBuffer.AsSpan(stripStart, ptr - stripStart),
                            PrinterWidthTiles,
                            palette);
                        if (image is not null)
                        {
                            images.Add(image);
                        }

                        // Non-zero "feed after" terminates the image group;
                        // either way, advance the strip window so the next PRINT
                        // starts fresh.
                        _ = margins;
                        stripStart = ptr;
                        break;
                    }

                case CommandTransfer:
                    {
                        if (idx + 2 > data.Length)
                        {
                            idx = data.Length;
                            break;
                        }

                        int len = data[idx++] | (data[idx++] << 8);
                        int available = Math.Min(len, data.Length - idx);

                        int before = ptr;
                        ptr = CopyUncompressed(data, idx, available, tileBuffer, ptr);
                        idx += available;
                        if (available < len)
                        {
                            // Truncated payload — flush what we got and stop.
                            idx = data.Length;
                        }

                        Image<Rgba32>? image = FlushStrip(
                            tileBuffer.AsSpan(before, ptr - before),
                            CameraWidthTiles,
                            palette: 0xE4);
                        if (image is not null)
                        {
                            images.Add(image);
                        }

                        stripStart = ptr;
                        break;
                    }

                case CommandData:
                    {
                        if (idx + 3 > data.Length)
                        {
                            idx = data.Length;
                            break;
                        }

                        bool compressed = data[idx++] != 0;
                        int len = data[idx++] | (data[idx++] << 8);
                        int available = Math.Min(len, data.Length - idx);

                        ptr = compressed
                            ? DecodeCompressed(data, idx, available, tileBuffer, ptr)
                            : CopyUncompressed(data, idx, available, tileBuffer, ptr);
                        idx += available;
                        if (available < len)
                        {
                            // Truncated payload — accept what we got and stop.
                            idx = data.Length;
                        }
                        break;
                    }

                default:
                    // Unknown / synchronisation byte: terminate cleanly, mirroring
                    // the TS decoder's behaviour.
                    idx = data.Length;
                    break;
            }
        }

        // A capture that ended without a PRINT flush (e.g. COMMAND_DATA only)
        // still has usable tile data — emit it as one more image using the
        // printer width. The Cardputer saves partial streams this way.
        if (ptr > stripStart)
        {
            Image<Rgba32>? tail = FlushStrip(
                tileBuffer.AsSpan(stripStart, ptr - stripStart),
                PrinterWidthTiles,
                palette: 0xE4);
            if (tail is not null)
            {
                images.Add(tail);
            }
        }

        return images;
    }

    private static int CopyUncompressed(ReadOnlySpan<byte> src, int srcPtr, int len, byte[] dest, int destPtr)
    {
        int capacity = dest.Length - destPtr;
        int take = Math.Min(len, Math.Max(capacity, 0));
        if (take > 0)
        {
            src.Slice(srcPtr, take).CopyTo(dest.AsSpan(destPtr));
        }
        return destPtr + take;
    }

    private static int DecodeCompressed(ReadOnlySpan<byte> src, int srcPtr, int len, byte[] dest, int destPtr)
    {
        int stop = srcPtr + len;
        while (srcPtr < stop)
        {
            byte tag = src[srcPtr++];
            if ((tag & 0x80) != 0)
            {
                if (srcPtr >= stop)
                {
                    // Truncated RLE run byte: stop and keep what we have.
                    break;
                }

                byte value = src[srcPtr++];
                int count = (tag & 0x7F) + 2;
                int capacity = dest.Length - destPtr;
                int take = Math.Min(count, Math.Max(capacity, 0));
                if (take > 0)
                {
                    dest.AsSpan(destPtr, take).Fill(value);
                    destPtr += take;
                }
                if (take < count)
                {
                    break;
                }
            }
            else
            {
                int count = tag + 1;
                int available = Math.Min(count, stop - srcPtr);
                int capacity = dest.Length - destPtr;
                int take = Math.Min(available, Math.Max(capacity, 0));
                if (take > 0)
                {
                    src.Slice(srcPtr, take).CopyTo(dest.AsSpan(destPtr));
                    destPtr += take;
                }
                srcPtr += available;
                if (take < count)
                {
                    break;
                }
            }
        }

        return destPtr;
    }

    private static Image<Rgba32>? FlushStrip(ReadOnlySpan<byte> tiles, int widthInTiles, byte palette)
    {
        if (tiles.IsEmpty)
        {
            return null;
        }

        int tileCount = tiles.Length / TileByteCount;
        if (tileCount == 0)
        {
            return null;
        }

        // Truncated streams (Cardputer captures, partial transfers) often end
        // mid-row. Round up to the next full row so a partial strip still
        // renders something useful instead of being dropped.
        int heightInTiles = (tileCount + widthInTiles - 1) / widthInTiles;
        int paddedTileCount = heightInTiles * widthInTiles;
        byte[] padded;
        ReadOnlySpan<byte> source;
        if (paddedTileCount == tileCount)
        {
            padded = [];
            source = tiles;
        }
        else
        {
            padded = new byte[paddedTileCount * TileByteCount];
            tiles.CopyTo(padded);
            // Remainder stays zero (renders as colour 0 / lightest shade).
            source = padded;
        }

        Rgba32[] lut =
        [
            DefaultPalette[palette & 0x03],
            DefaultPalette[(palette >> 2) & 0x03],
            DefaultPalette[(palette >> 4) & 0x03],
            DefaultPalette[(palette >> 6) & 0x03],
        ];

        Image<Rgba32> image = new(widthInTiles * TilePixelSize, heightInTiles * TilePixelSize);
        for (int tileIndex = 0; tileIndex < paddedTileCount; tileIndex++)
        {
            int tileX = tileIndex % widthInTiles;
            int tileY = tileIndex / widthInTiles;
            ReadOnlySpan<byte> tile = source.Slice(tileIndex * TileByteCount, TileByteCount);

            for (int y = 0; y < TilePixelSize; y++)
            {
                byte low = tile[y * 2];
                byte high = tile[(y * 2) + 1];

                for (int x = 0; x < TilePixelSize; x++)
                {
                    int shift = 7 - x;
                    int colorIndex = (((high >> shift) & 0x01) << 1) | ((low >> shift) & 0x01);
                    image[(tileX * TilePixelSize) + x, (tileY * TilePixelSize) + y] = lut[colorIndex];
                }
            }
        }

        return image;
    }
}
