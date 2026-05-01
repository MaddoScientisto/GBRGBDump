using System.Text;
using GBTools.ImageSharp.GameBoyCamera.Codec;
using GBTools.ImageSharp.GameBoyCamera.Metadata;
using GBTools.ImageSharp.GameBoyCamera.Model;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace GBTools.ImageSharp.GameBoyCamera.Compatibility;

public static class GameBoyCameraCompatibility
{
    public static Image<Rgba32> LoadSaveDump(Stream stream, GameBoyCameraLoadOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        options ??= new GameBoyCameraLoadOptions { SourceKind = GameBoyCameraSourceKind.SaveDump };
        return GameBoyCameraImageCodec.RenderAlbum(LoadSaveAlbum(stream, options), options.Palette);
    }

    public static GbcAlbum LoadSaveAlbum(Stream stream, GameBoyCameraLoadOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        options ??= new GameBoyCameraLoadOptions { SourceKind = GameBoyCameraSourceKind.SaveDump };
        byte[] data = ReadAllBytes(stream);

        if (TryLoadSinglePhotoTileAlbum(data, options.SourceKind, options.FrameMode, out GbcAlbum? rawTileAlbum))
        {
            return rawTileAlbum;
        }

        ValidateMagic(data, options.ForceMagicCheck);

        List<int> addresses = Enumerable.Range(0, (int)Math.Ceiling(data.Length / 4096d))
            .Select(index => index * 0x1000)
            .ToList();

        if (data.Length == 0x100000)
        {
            string romName = Encoding.ASCII.GetString(data.Skip(0x134).Take(0x10).Where(static b => b is not 0 and not 128).ToArray()).Trim();
            if (string.Equals(romName, "PHOTO", StringComparison.Ordinal))
            {
                addresses = addresses.Skip(32).ToList();
            }
        }

        addresses = addresses.Where(address => ((address - 0x1000) % 0x20000) != 0).ToList();
        if (!options.IncludeLastSeen)
        {
            addresses = addresses.Where(address => (address % 0x20000) != 0).ToList();
        }

        List<GbcPhoto> photos = [];
        foreach (int address in addresses)
        {
            if (!GameBoyCameraRawPhotoTransform.TryTransformFramedPhoto(data, address, out GbcTileGrid? framedGrid) || framedGrid is null)
            {
                continue;
            }

            GameBoyCameraFrameMetadata? metadata = TryCreateSaveMetadata(data, address, options.CartIsJapanese);
            if (metadata?.AlbumIndex == 255 && !options.IncludeDeleted)
            {
                continue;
            }

            GbcFrameOverlay? frameOverlay = options.FrameMode == GameBoyCameraFrameMode.Extract
                ? GameBoyCameraFrameCodec.ExtractFrameOverlay(framedGrid, 2)
                : null;
            ReadOnlyMemory<byte>? thumbnailBytes = address + 0x0E00 + GbcThumbnail.ByteLength <= data.Length
                ? data.AsMemory(address + 0x0E00, GbcThumbnail.ByteLength)
                : null;
            photos.Add(new GbcPhoto(
                ApplyFrameMode(framedGrid, options.FrameMode),
                frameOverlay,
                metadata,
                thumbnailBytes is null ? null : new GbcThumbnail(thumbnailBytes.Value)));
        }

        if (photos.Count == 0)
        {
            throw new InvalidDataException("Save dump did not contain any decodable photos.");
        }

        IReadOnlyList<GbcPhoto> orderedPhotos = options.ImportOrdering == GameBoyCameraImportOrdering.Ram
            ? photos
            : photos.OrderBy(static photo => photo.Metadata?.AlbumIndex ?? int.MaxValue).ToArray();

        string[] romTypes = orderedPhotos
            .Select(static photo => photo.Metadata?.RomType)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        string? romType = romTypes.Length == 1 ? romTypes[0] : null;
        GameBoyCameraAlbumMetadata albumMetadata = new(
            options.SourceKind,
            romType,
            0,
            options.ImportOrdering.ToString().ToLowerInvariant(),
            null);

        return new GbcAlbum(albumMetadata, orderedPhotos);
    }

    public static Task ExportGbPrinterWebJsonAsync(Image image, Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(stream);

        return GameBoyCameraJsonCompatibilityCodec.ExportAsync(image, stream, cancellationToken);
    }

    public static Task ExportGbPrinterWebJsonAsync(GbcAlbum album, Stream stream, GameBoyCameraJsonExportOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(album);
        ArgumentNullException.ThrowIfNull(stream);

        return GameBoyCameraJsonCompatibilityCodec.ExportAlbumAsync(album, stream, options, cancellationToken);
    }

    public static void ExportGbBin(Image image, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(stream);

        using Image<Rgba32> rgba = image.CloneAs<Rgba32>();
        GbcTileGrid grid = GameBoyCameraImageCodec.EncodeToTileGrid(rgba, GameBoyCameraPalette.Default);
        stream.Write("GB-BIN01"u8);
        foreach (GbcTile2Bpp tile in grid.Tiles)
        {
            stream.Write(tile.Bytes.Span);
        }
    }

    public static void ExportTxt(Image image, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(stream);

        using Image<Rgba32> rgba = image.CloneAs<Rgba32>();
        GbcTileGrid grid = GameBoyCameraImageCodec.EncodeToTileGrid(rgba, GameBoyCameraPalette.Default);
        string payload = string.Join(
            "\n",
            GameBoyCameraImageCodec.FormatTiles(grid)
                .Select(static tile => tile.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant()));

        using StreamWriter writer = new(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);
        writer.Write(payload);
        writer.Flush();
    }

    public static void ExportGbBinBase64(Image image, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(stream);

        using MemoryStream binaryStream = new();
        ExportGbBin(image, binaryStream);

        string payload = Convert.ToBase64String(binaryStream.ToArray());
        using StreamWriter writer = new(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);
        writer.Write(payload);
        writer.Flush();
    }

    public static Image<Rgba32> LoadRomDump(Stream stream, GameBoyCameraLoadOptions? options = null)
    {
        options ??= new GameBoyCameraLoadOptions { SourceKind = GameBoyCameraSourceKind.RomDump };
        return GameBoyCameraImageCodec.RenderAlbum(LoadRomAlbum(stream, options), options.Palette);
    }

    public static GbcAlbum LoadRomAlbum(Stream stream, GameBoyCameraLoadOptions? options = null)
    {
        options ??= new GameBoyCameraLoadOptions { SourceKind = GameBoyCameraSourceKind.RomDump };
        return LoadSaveAlbum(stream, options);
    }

    public static Image<Rgba32> LoadGbBin(Stream stream, GameBoyCameraLoadOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        options ??= new GameBoyCameraLoadOptions { SourceKind = GameBoyCameraSourceKind.GbBin };
        return GameBoyCameraImageCodec.RenderAlbum(LoadGbBinAlbum(stream, options), options.Palette);
    }

    public static GbcAlbum LoadGbBinAlbum(Stream stream, GameBoyCameraLoadOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        options ??= new GameBoyCameraLoadOptions { SourceKind = GameBoyCameraSourceKind.GbBin };

        byte[] data = ReadAllBytes(stream);
        ReadOnlySpan<byte> header = "GB-BIN01"u8;
        if (data.Length < header.Length || !data.AsSpan(0, header.Length).SequenceEqual(header))
        {
            throw new InvalidDataException("Not a valid GB-BIN01 file.");
        }

        byte[] tileBytes = data.AsSpan(header.Length).ToArray();
        if (tileBytes.Length % GameBoyCameraConstants.TileByteCount != 0)
        {
            int paddedLength = ((tileBytes.Length / GameBoyCameraConstants.TileByteCount) + 1) * GameBoyCameraConstants.TileByteCount;
            Array.Resize(ref tileBytes, paddedLength);
            tileBytes.AsSpan(data.Length - header.Length).Fill(0xFF);
        }

        int tileCount = tileBytes.Length / GameBoyCameraConstants.TileByteCount;
        int preferredWidth = InferGbBinWidthInTiles(tileCount);

        GbcTileGrid grid = GameBoyCameraImageCodec.ParseBinaryTilePayload(tileBytes, preferredWidth);
        if (grid.WidthInTiles == GameBoyCameraConstants.FramedPhotoTileWidth && options.FrameMode != GameBoyCameraFrameMode.Keep)
        {
            grid = GameBoyCameraFrameCodec.StripFrame(grid, 2);
        }

        return new GbcAlbum(
            new GameBoyCameraAlbumMetadata(GameBoyCameraSourceKind.GbBin, null, 0, null, null),
            [new GbcPhoto(grid, null, null, null)]);
    }

    public static Image<Rgba32> LoadGbPrinterWebJson(Stream stream, GameBoyCameraLoadOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        options ??= new GameBoyCameraLoadOptions { SourceKind = GameBoyCameraSourceKind.GbPrinterWebJson };
        return GameBoyCameraImageCodec.RenderAlbum(LoadGbPrinterWebAlbum(stream, options), options.Palette);
    }

    public static GbcAlbum LoadGbPrinterWebAlbum(Stream stream, GameBoyCameraLoadOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        options ??= new GameBoyCameraLoadOptions { SourceKind = GameBoyCameraSourceKind.GbPrinterWebJson };
        return GameBoyCameraJsonCompatibilityCodec.LoadAlbum(stream, options);
    }

    private static void ValidateMagic(byte[] data, bool forceMagicCheck)
    {
        if (!forceMagicCheck)
        {
            return;
        }

        int[] magicPlaces = [0x10D2, 0x11AB, 0x11D0, 0x11F5];
        foreach (int offset in magicPlaces)
        {
            if (data.Length < offset + 5 || Encoding.ASCII.GetString(data, offset, 5) != "Magic")
            {
                throw new InvalidDataException("Save dump failed the Magic signature check.");
            }
        }
    }

    private static GameBoyCameraFrameMetadata? TryCreateSaveMetadata(byte[] data, int baseAddress, bool cartIsJapanese)
    {
        if (baseAddress + 0x0FFF >= data.Length)
        {
            return null;
        }

        int inBankAddress = baseAddress % 0x20000;
        int bankIndex = baseAddress / 0x20000;
        int bankStartAddress = bankIndex * 0x20000;
        int cartIndex = (inBankAddress / 0x1000) - 2;
        int albumIndex = cartIndex >= 0 && bankStartAddress + 0x11B2 + cartIndex < data.Length
            ? data[bankStartAddress + 0x11B2 + cartIndex] + (bankIndex * 30)
            : -1;
        int frameNumber = data[baseAddress + 0x0F54];
        ReadOnlySpan<byte> thumbnail = data.AsSpan(baseAddress + 0x0E00, GbcThumbnail.ByteLength);
        GameBoyCameraRomType romType = GameBoyCameraMetadataCodec.DetectRomType(thumbnail);
        GameBoyCameraBasicMetadata? basic = romType == GameBoyCameraRomType.Stock
            ? GameBoyCameraMetadataCodec.ParseBasicMetadata(data, baseAddress, cartIsJapanese)
            : null;
        GameBoyCameraCustomMetadata? custom = romType != GameBoyCameraRomType.Stock
            ? GameBoyCameraMetadataCodec.ParseCustomMetadata(thumbnail, romType)
            : null;

        return new GameBoyCameraFrameMetadata(
            albumIndex,
            cartIndex,
            baseAddress,
            frameNumber,
            basic?.UserId,
            basic?.UserName,
            basic?.BirthDate,
            basic?.Gender,
            basic?.BloodType,
            basic?.Comment,
            basic?.IsCopy ?? false,
            romType.ToString().ToLowerInvariant(),
            custom?.Exposure,
            custom?.CaptureMode,
            custom?.EdgeExclusive,
            custom?.EdgeOperation,
            custom?.EdgeMode,
            custom?.Gain,
            custom?.InvertOutput,
            custom?.VoltageReference,
            custom?.ZeroPoint,
            custom?.VoltageOutput,
            custom?.DitherSet,
            custom?.Contrast);
    }

    private static GbcTileGrid ApplyFrameMode(GbcTileGrid grid, GameBoyCameraFrameMode frameMode) =>
        grid.WidthInTiles == GameBoyCameraConstants.FramedPhotoTileWidth && frameMode != GameBoyCameraFrameMode.Keep
            ? GameBoyCameraFrameCodec.StripFrame(grid, 2)
            : grid;

    private static byte[] ReadAllBytes(Stream stream)
    {
        if (stream is MemoryStream memoryStream && memoryStream.TryGetBuffer(out ArraySegment<byte> buffer))
        {
            return buffer.AsSpan().ToArray();
        }

        using MemoryStream copy = new();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    private static int InferGbBinWidthInTiles(int tileCount)
    {
        if (tileCount == GameBoyCameraConstants.RawPhotoTileWidth * GameBoyCameraConstants.RawPhotoTileHeight)
        {
            return GameBoyCameraConstants.RawPhotoTileWidth;
        }

        if (tileCount == GameBoyCameraConstants.FramedPhotoTileWidth * GameBoyCameraConstants.FramedPhotoTileHeight)
        {
            return GameBoyCameraConstants.FramedPhotoTileWidth;
        }

        int minimumCandidate = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(tileCount)));
        for (int candidate = minimumCandidate; candidate <= tileCount; candidate++)
        {
            if (tileCount % candidate == 0)
            {
                return candidate;
            }
        }

        return tileCount;
    }

    private static bool TryLoadSinglePhotoTileAlbum(byte[] data, GameBoyCameraSourceKind sourceKind, GameBoyCameraFrameMode frameMode, out GbcAlbum? album)
    {
        album = null;

        if (data.Length % GameBoyCameraConstants.TileByteCount != 0)
        {
            return false;
        }

        int tileCount = data.Length / GameBoyCameraConstants.TileByteCount;
        if (tileCount != GameBoyCameraConstants.RawPhotoTileWidth * GameBoyCameraConstants.RawPhotoTileHeight
            && tileCount != GameBoyCameraConstants.FramedPhotoTileWidth * GameBoyCameraConstants.FramedPhotoTileHeight)
        {
            return false;
        }

        GbcTileGrid grid = GameBoyCameraImageCodec.ParseBinaryTilePayload(data, InferGbBinWidthInTiles(tileCount));
        if (grid.WidthInTiles == GameBoyCameraConstants.FramedPhotoTileWidth && frameMode != GameBoyCameraFrameMode.Keep)
        {
            grid = GameBoyCameraFrameCodec.StripFrame(grid, 2);
        }

        album = new GbcAlbum(
            new GameBoyCameraAlbumMetadata(sourceKind, null, 1, "single-photo-tile-export", null),
            [new GbcPhoto(grid, null, null, null)]);
        return true;
    }
}