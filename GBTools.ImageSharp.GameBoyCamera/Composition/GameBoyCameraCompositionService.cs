using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using GBTools.ImageSharp.GameBoyCamera.Codec;
using GBTools.ImageSharp.GameBoyCamera.Model;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace GBTools.ImageSharp.GameBoyCamera.Composition;

public enum GameBoyCameraCompositionChannelOrder
{
    Sequential,
    Interleaved,
}

public enum GameBoyCameraAverageCompositionMode
{
    Normal,
    FullBank,
}

public sealed record GameBoyCameraRgbCompositionOptions(
    GameBoyCameraCompositionChannelOrder ChannelOrder = GameBoyCameraCompositionChannelOrder.Sequential);

public sealed record GameBoyCameraAverageCompositionOptions(
    GameBoyCameraCompositionChannelOrder ChannelOrder = GameBoyCameraCompositionChannelOrder.Sequential,
    GameBoyCameraAverageCompositionMode Mode = GameBoyCameraAverageCompositionMode.Normal,
    int PreferredSourceGroupSize = 15);

public sealed record GameBoyCameraAverageCompositionResult(
    IReadOnlyList<GbcPhoto> RgbPhotos,
    IReadOnlyList<GbcPhoto> AveragePhotos,
    IReadOnlyList<int> SourceGroupSizes);

public static class GameBoyCameraCompositionService
{
    private const string AverageAlgorithm = "alpha-stack-v1";

    public static IReadOnlyList<GbcPhoto> CreateRgbPhotos(IReadOnlyList<GbcPhoto> sourcePhotos, GameBoyCameraRgbCompositionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(sourcePhotos);
        options ??= new GameBoyCameraRgbCompositionOptions();

        if (sourcePhotos.Count < 3 || sourcePhotos.Count % 3 != 0)
        {
            throw new ArgumentException("RGB composition requires a photo count divisible by 3.", nameof(sourcePhotos));
        }

        List<GbcPhoto> results = [];
        int outputCount = sourcePhotos.Count / 3;
        IReadOnlyList<GbcRgbnChannelData> channelData = sourcePhotos.Select(CreateChannelData).ToArray();

        for (int index = 0; index < outputCount; index++)
        {
            int redIndex = GetChannelSourceIndex(options.ChannelOrder, outputCount, index, 0);
            int greenIndex = GetChannelSourceIndex(options.ChannelOrder, outputCount, index, 1);
            int blueIndex = GetChannelSourceIndex(options.ChannelOrder, outputCount, index, 2);

            GbcRgbnChannelData redChannel = channelData[redIndex];
            GbcRgbnChannelData greenChannel = channelData[greenIndex];
            GbcRgbnChannelData blueChannel = channelData[blueIndex];

            using Image<Rgba32> rendered = RenderRgbPhoto(sourcePhotos[redIndex], sourcePhotos[greenIndex], sourcePhotos[blueIndex]);
            GbcTileGrid tileGrid = TryEncodeRenderedGrid(rendered);
            GbcThumbnail thumbnail = new(GameBoyCameraImageCodec.CreateThumbnailBytes(rendered));

            results.Add(new GbcPhoto(
                tileGrid,
                null,
                null,
                thumbnail,
                GameBoyCameraImageCodec.CopyPixelData(rendered),
                rendered.Width,
                rendered.Height,
                new GbcRgbnData(
                    ComputeRgbnCompositeHash(redChannel, greenChannel, blueChannel, null),
                    redChannel,
                    greenChannel,
                    blueChannel,
                    null,
                    DefaultShades(),
                    DefaultShades(),
                    DefaultShades(),
                    DefaultShades(),
                    "multiply")));
        }

        return results;
    }

    public static GameBoyCameraAverageCompositionResult CreateAveragePhotos(IReadOnlyList<GbcPhoto> sourcePhotos, GameBoyCameraAverageCompositionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(sourcePhotos);
        options ??= new GameBoyCameraAverageCompositionOptions();

        if (options.PreferredSourceGroupSize < 3 || options.PreferredSourceGroupSize % 3 != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Average composition preferred group size must be divisible by 3.");
        }

        if (sourcePhotos.Count < 3 || sourcePhotos.Count % 3 != 0)
        {
            throw new ArgumentException("Average composition requires a photo count divisible by 3.", nameof(sourcePhotos));
        }

        IReadOnlyList<IReadOnlyList<GbcPhoto>> sourceGroups = PartitionAverageSourcePhotos(sourcePhotos, options.PreferredSourceGroupSize);
        List<GbcPhoto> rgbPhotos = [];
        List<GbcPhoto> averagePhotos = [];
        List<GbcAverageSourceGroup> averageSourceGroups = [];

        foreach (IReadOnlyList<GbcPhoto> group in sourceGroups)
        {
            IReadOnlyList<GbcPhoto> groupRgbPhotos = CreateRgbPhotos(group, new GameBoyCameraRgbCompositionOptions(options.ChannelOrder));
            GbcAverageSourceGroup rawSourceGroup = CreateAverageSourceGroup(group);

            rgbPhotos.AddRange(groupRgbPhotos);
            averageSourceGroups.Add(rawSourceGroup);
            averagePhotos.Add(CreateAveragePhoto(groupRgbPhotos, [rawSourceGroup], options.ChannelOrder));
        }

        if (options.Mode == GameBoyCameraAverageCompositionMode.FullBank && rgbPhotos.Count > 0)
        {
            averagePhotos.Add(CreateAveragePhoto(rgbPhotos, averageSourceGroups, options.ChannelOrder));
        }

        return new GameBoyCameraAverageCompositionResult(
            rgbPhotos,
            averagePhotos,
            sourceGroups.Select(static group => group.Count).ToArray());
    }

    public static IReadOnlyList<int> GetAverageSourceGroupSizes(int sourcePhotoCount, int preferredSourceGroupSize = 15)
    {
        if (sourcePhotoCount < 3 || sourcePhotoCount % 3 != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourcePhotoCount), "Average composition requires a photo count divisible by 3.");
        }

        if (preferredSourceGroupSize < 3 || preferredSourceGroupSize % 3 != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(preferredSourceGroupSize), "Average composition preferred group size must be divisible by 3.");
        }

        List<int> groupSizes = [];
        int remainingCount = sourcePhotoCount;
        while (remainingCount > 0)
        {
            int nextGroupSize = GetNextAverageGroupSize(remainingCount, preferredSourceGroupSize);
            groupSizes.Add(nextGroupSize);
            remainingCount -= nextGroupSize;
        }

        return groupSizes;
    }

    public static GbcPhoto CreateAveragePhotoFromSourceGroups(
        IReadOnlyList<IReadOnlyList<GbcPhoto>> sourceGroups,
        GameBoyCameraCompositionChannelOrder channelOrder = GameBoyCameraCompositionChannelOrder.Sequential)
    {
        ArgumentNullException.ThrowIfNull(sourceGroups);

        if (sourceGroups.Count == 0 || sourceGroups.Any(static group => group.Count == 0))
        {
            throw new ArgumentException("Average composition requires at least one non-empty source group.", nameof(sourceGroups));
        }

        List<GbcPhoto> rgbPhotos = [];
        List<GbcAverageSourceGroup> averageSourceGroups = [];

        foreach (IReadOnlyList<GbcPhoto> sourceGroup in sourceGroups)
        {
            if (sourceGroup.Count < 3 || sourceGroup.Count % 3 != 0)
            {
                throw new ArgumentException("Each average composition source group must be divisible by 3.", nameof(sourceGroups));
            }

            rgbPhotos.AddRange(CreateRgbPhotos(sourceGroup, new GameBoyCameraRgbCompositionOptions(channelOrder)));
            averageSourceGroups.Add(CreateAverageSourceGroup(sourceGroup));
        }

        return CreateAveragePhoto(rgbPhotos, averageSourceGroups, channelOrder);
    }

    public static GbcPhoto CreateDirectAveragePhoto(IReadOnlyList<GbcPhoto> sourcePhotos)
    {
        ArgumentNullException.ThrowIfNull(sourcePhotos);

        if (sourcePhotos.Count < 3)
        {
            throw new ArgumentException("Average composition requires at least 3 source photos.", nameof(sourcePhotos));
        }

        using Image<Rgba32> rendered = RenderAveragePhoto(sourcePhotos);
        GbcTileGrid tileGrid = TryEncodeRenderedGrid(rendered);
        GbcThumbnail thumbnail = new(GameBoyCameraImageCodec.CreateThumbnailBytes(rendered));
        GbcAverageSourceGroup sourceGroup = CreateAverageSourceGroup(sourcePhotos);

        return new GbcPhoto(
            tileGrid,
            null,
            null,
            thumbnail,
            GameBoyCameraImageCodec.CopyPixelData(rendered),
            rendered.Width,
            rendered.Height,
            null,
            new GbcAverageData(
                ComputeAverageCompositeHash([sourceGroup], GameBoyCameraCompositionChannelOrder.Sequential),
                [sourceGroup],
                GameBoyCameraCompositionChannelOrder.Sequential,
                AverageAlgorithm,
                GameBoyCameraAverageCompositionPipeline.Direct));
    }

    private static IReadOnlyList<IReadOnlyList<GbcPhoto>> PartitionAverageSourcePhotos(IReadOnlyList<GbcPhoto> sourcePhotos, int preferredGroupSize)
    {
        List<IReadOnlyList<GbcPhoto>> groups = [];
        int currentIndex = 0;
        int remainingCount = sourcePhotos.Count;

        while (remainingCount > 0)
        {
            int nextGroupSize = GetNextAverageGroupSize(remainingCount, preferredGroupSize);
            groups.Add(sourcePhotos.Skip(currentIndex).Take(nextGroupSize).ToArray());
            currentIndex += nextGroupSize;
            remainingCount -= nextGroupSize;
        }

        return groups;
    }

    private static int GetNextAverageGroupSize(int remainingCount, int preferredGroupSize)
    {
        if (remainingCount < 3 || remainingCount % 3 != 0)
        {
            throw new ArgumentException("Average composition requires a photo count divisible by 3.", nameof(remainingCount));
        }

        if (remainingCount <= preferredGroupSize)
        {
            return remainingCount;
        }

        for (int groupSize = preferredGroupSize; groupSize >= 3; groupSize -= 3)
        {
            int nextRemaining = remainingCount - groupSize;
            if (nextRemaining == 0 || (nextRemaining >= 3 && nextRemaining % 3 == 0))
            {
                return groupSize;
            }
        }

        return remainingCount;
    }

    private static int GetChannelSourceIndex(GameBoyCameraCompositionChannelOrder channelOrder, int outputCount, int outputIndex, int channelIndex)
    {
        return channelOrder switch
        {
            GameBoyCameraCompositionChannelOrder.Sequential => outputIndex + (outputCount * channelIndex),
            GameBoyCameraCompositionChannelOrder.Interleaved => channelIndex + (3 * outputIndex),
            _ => throw new ArgumentOutOfRangeException(nameof(channelOrder)),
        };
    }

    private static Image<Rgba32> RenderRgbPhoto(GbcPhoto redPhoto, GbcPhoto greenPhoto, GbcPhoto bluePhoto)
    {
        using Image<Rgba32> redImage = GameBoyCameraImageCodec.RenderPhoto(redPhoto);
        using Image<Rgba32> greenImage = GameBoyCameraImageCodec.RenderPhoto(greenPhoto);
        using Image<Rgba32> blueImage = GameBoyCameraImageCodec.RenderPhoto(bluePhoto);

        int width = Math.Max(redImage.Width, Math.Max(greenImage.Width, blueImage.Width));
        int height = Math.Max(redImage.Height, Math.Max(greenImage.Height, blueImage.Height));
        Image<Rgba32> result = new(width, height);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                byte red = GetIntensity(redImage, x, y);
                byte green = GetIntensity(greenImage, x, y);
                byte blue = GetIntensity(blueImage, x, y);
                result[x, y] = new Rgba32(red, green, blue, 255);
            }
        }

        return result;
    }

    private static Image<Rgba32> RenderAveragePhoto(IReadOnlyList<GbcPhoto> sourcePhotos)
    {
        List<Image<Rgba32>> renderedSources = sourcePhotos.Select(static photo => GameBoyCameraImageCodec.RenderPhoto(photo)).ToList();

        try
        {
            int width = renderedSources.Max(static image => image.Width);
            int height = renderedSources.Max(static image => image.Height);
            Image<Rgba32> result = new(width, height);

            for (int index = 0; index < renderedSources.Count; index++)
            {
                Image<Rgba32> source = renderedSources[index];
                float alpha = 1f / (index + 1);

                for (int y = 0; y < source.Height; y++)
                {
                    for (int x = 0; x < source.Width; x++)
                    {
                        Rgba32 destinationPixel = result[x, y];
                        Rgba32 sourcePixel = source[x, y];

                        result[x, y] = new Rgba32(
                            Blend(destinationPixel.R, sourcePixel.R, alpha),
                            Blend(destinationPixel.G, sourcePixel.G, alpha),
                            Blend(destinationPixel.B, sourcePixel.B, alpha),
                            255);
                    }
                }
            }

            return result;
        }
        finally
        {
            foreach (Image<Rgba32> image in renderedSources)
            {
                image.Dispose();
            }
        }
    }

    private static byte Blend(byte destination, byte source, float alpha)
        => (byte)Math.Clamp((int)Math.Round((source * alpha) + (destination * (1f - alpha))), 0, 255);

    private static byte GetIntensity(Image<Rgba32> image, int x, int y)
    {
        if ((uint)x >= (uint)image.Width || (uint)y >= (uint)image.Height)
        {
            return 0;
        }

        Rgba32 pixel = image[x, y];
        return (byte)Math.Clamp((pixel.R + pixel.G + pixel.B) / 3, 0, 255);
    }

    private static GbcTileGrid TryEncodeRenderedGrid(Image<Rgba32> rendered)
    {
        try
        {
            return GameBoyCameraImageCodec.EncodeToTileGrid(rendered, GameBoyCameraPalette.Default);
        }
        catch (ArgumentException)
        {
            int widthInTiles = Math.Max(1, (int)Math.Ceiling(rendered.Width / (double)GameBoyCameraConstants.TilePixelWidth));
            int heightInTiles = Math.Max(1, (int)Math.Ceiling(rendered.Height / (double)GameBoyCameraConstants.TilePixelHeight));
            return GameBoyCameraTileGridFactory.CreateFromBinary(new byte[widthInTiles * heightInTiles * GameBoyCameraConstants.TileByteCount], widthInTiles);
        }
    }

    private static GbcRgbnChannelData CreateChannelData(GbcPhoto photo)
    {
        GbcTileGrid grid = CreateExportGrid(photo);
        string rawTiles = string.Join("\n", GameBoyCameraImageCodec.FormatTiles(grid).Select(static tile => tile.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant()));
        string compressedPayload = DeflateLatin1String(rawTiles);
        return new GbcRgbnChannelData(ComputeHash(compressedPayload), compressedPayload);
    }

    private static GbcAverageSourceGroup CreateAverageSourceGroup(IReadOnlyList<GbcPhoto> sourcePhotos)
        => new(sourcePhotos.Select(CreateAverageSourcePhoto).ToArray());

    private static GbcAverageSourcePhoto CreateAverageSourcePhoto(GbcPhoto photo)
    {
        GbcTileGrid grid = CreateExportGrid(photo);
        GbcRgbnChannelData channelData = CreateChannelData(photo);
        return new GbcAverageSourcePhoto(channelData.Hash, grid.Tiles.Count, channelData.CompressedPayload);
    }

    private static GbcPhoto CreateAveragePhoto(
        IReadOnlyList<GbcPhoto> rgbPhotos,
        IReadOnlyList<GbcAverageSourceGroup> sourceGroups,
        GameBoyCameraCompositionChannelOrder channelOrder)
    {
        using Image<Rgba32> rendered = RenderAveragePhoto(rgbPhotos);
        GbcTileGrid tileGrid = TryEncodeRenderedGrid(rendered);
        GbcThumbnail thumbnail = new(GameBoyCameraImageCodec.CreateThumbnailBytes(rendered));

        return new GbcPhoto(
            tileGrid,
            null,
            null,
            thumbnail,
            GameBoyCameraImageCodec.CopyPixelData(rendered),
            rendered.Width,
            rendered.Height,
            null,
            new GbcAverageData(
                ComputeAverageCompositeHash(sourceGroups, channelOrder),
                sourceGroups,
                channelOrder,
                AverageAlgorithm,
                GameBoyCameraAverageCompositionPipeline.Rgb));
    }

    private static GbcTileGrid CreateExportGrid(GbcPhoto photo)
    {
        if (!photo.HasRenderedImage)
        {
            return photo.TileGrid;
        }

        using Image<Rgba32> rendered = GameBoyCameraImageCodec.RenderPhoto(photo);
        return TryEncodeRenderedGrid(rendered);
    }

    private static string GetPhotoHash(GbcPhoto photo)
    {
        if (photo.AverageData is not null)
        {
            return photo.AverageData.CompositeHash;
        }

        if (photo.RgbnData is not null)
        {
            return photo.RgbnData.CompositeHash;
        }

        return CreateChannelData(photo).Hash;
    }

    private static string ComputeAverageCompositeHash(IReadOnlyList<GbcAverageSourceGroup> sourceGroups, GameBoyCameraCompositionChannelOrder channelOrder)
    {
        string groupPayload = string.Join("||", sourceGroups.Select(static group => string.Join('|', group.SourcePhotos.Select(static source => source.Hash))));
        byte[] bytes = Encoding.UTF8.GetBytes($"average|{AverageAlgorithm}|{channelOrder}|{groupPayload}");
        byte[] hash = SHA1.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string ComputeRgbnCompositeHash(GbcRgbnChannelData? red, GbcRgbnChannelData? green, GbcRgbnChannelData? blue, GbcRgbnChannelData? neutral)
    {
        string[] parts =
        [
            $"r:{red?.Hash ?? string.Empty}",
            $"g:{green?.Hash ?? string.Empty}",
            $"b:{blue?.Hash ?? string.Empty}",
            $"n:{neutral?.Hash ?? string.Empty}",
        ];

        byte[] bytes = Encoding.UTF8.GetBytes(string.Join("|", parts));
        byte[] hash = SHA1.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string DeflateLatin1String(string payload)
    {
        using MemoryStream output = new();
        using (ZLibStream zlib = new(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
            zlib.Write(payloadBytes, 0, payloadBytes.Length);
        }

        return Encoding.Latin1.GetString(output.ToArray());
    }

    private static string ComputeHash(string payload)
    {
        byte[] bytes = Encoding.Latin1.GetBytes(payload);
        byte[] hash = SHA1.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static IReadOnlyList<byte> DefaultShades() => [0, 85, 170, 255];
}