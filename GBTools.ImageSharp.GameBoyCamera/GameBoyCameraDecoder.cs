using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;
using GBTools.ImageSharp.GameBoyCamera.Codec;
using GBTools.ImageSharp.GameBoyCamera.Model;

namespace GBTools.ImageSharp.GameBoyCamera;

public sealed class GameBoyCameraDecoder : ImageDecoder
{
    protected override Image Decode(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
        => Decode<Rgba32>(options, stream, cancellationToken);

    protected override Image<TPixel> Decode<TPixel>(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
    {
        using Image<Rgba32> rgba = DecodeCanonical(stream);
        return rgba.CloneAs<TPixel>();
    }

    protected override ImageInfo Identify(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
    {
        CanonicalHeader header = ReadHeader(stream);
        ImageMetadata metadata = new();

        return new ImageInfo(
            new PixelTypeInfo(32),
            new Size(
                header.WidthInTiles * GameBoyCameraConstants.TilePixelWidth,
                header.HeightInTiles * GameBoyCameraConstants.TilePixelHeight),
            metadata,
            Array.Empty<ImageFrameMetadata>());
    }

    internal static Image<Rgba32> DecodeCanonical(Stream stream)
    {
        CanonicalHeader header = ReadHeader(stream);
        byte[] tileBytes = new byte[header.TileDataLength];
        int bytesRead = stream.Read(tileBytes, 0, tileBytes.Length);
        if (bytesRead != tileBytes.Length)
        {
            throw new EndOfStreamException("Unexpected end of stream while reading tile payload.");
        }

        if (header.ThumbnailLength > 0)
        {
            stream.Position += header.ThumbnailLength;
        }

        GbcTileGrid grid = GameBoyCameraTileGridFactory.CreateFromBinary(tileBytes, header.WidthInTiles);
        return GameBoyCameraTileGridRenderer.Render(grid);
    }

    internal static CanonicalHeader ReadHeader(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        Span<byte> prefix = stackalloc byte[GameBoyCameraConstants.CanonicalMagicHeader.Length];
        if (stream.Read(prefix) != prefix.Length || !prefix.SequenceEqual(GameBoyCameraConstants.CanonicalMagicHeader))
        {
            throw new InvalidDataException("Stream is not a Game Boy Camera canonical image.");
        }

        Span<byte> buffer = stackalloc byte[14];
        if (stream.Read(buffer) != buffer.Length)
        {
            throw new EndOfStreamException("Unexpected end of stream while reading canonical header.");
        }

        byte version = buffer[0];
        byte flags = buffer[1];
        int widthInTiles = buffer[2];
        int heightInTiles = buffer[3];
        int tileDataLength = BitConverter.ToInt32(buffer[4..8]);
        int thumbnailLength = BitConverter.ToInt32(buffer[8..12]);
        int metadataLength = BitConverter.ToInt16(buffer[12..14]);

        if (version != 1)
        {
            throw new InvalidDataException($"Unsupported canonical format version {version}.");
        }

        return new CanonicalHeader(version, flags, widthInTiles, heightInTiles, tileDataLength, thumbnailLength, metadataLength);
    }

    internal readonly record struct CanonicalHeader(
        byte Version,
        byte Flags,
        int WidthInTiles,
        int HeightInTiles,
        int TileDataLength,
        int ThumbnailLength,
        int MetadataLength);
}