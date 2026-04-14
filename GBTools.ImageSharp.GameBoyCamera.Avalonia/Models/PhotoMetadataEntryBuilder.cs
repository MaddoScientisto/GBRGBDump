using GBTools.ImageSharp.GameBoyCamera.Metadata;
using GBTools.ImageSharp.GameBoyCamera.Model;

namespace GBTools.ImageSharp.GameBoyCamera.Avalonia.Models;

public static class PhotoMetadataEntryBuilder
{
    public static IReadOnlyList<MetadataEntry> Build(GbcPhoto photo, GameBoyCameraAlbumMetadata? albumMetadata, int index, string? sourceDescription = null)
    {
        List<MetadataEntry> entries =
        [
            new("Index", (index + 1).ToString()),
            new("Dimensions", $"{photo.TileGrid.WidthInTiles * 8}x{photo.TileGrid.HeightInTiles * 8}"),
            new("Source Kind", sourceDescription ?? albumMetadata?.SourceKind.ToString() ?? "Unknown"),
        ];

        Add(entries, "Source ROM", albumMetadata?.SourceRomType);
        Add(entries, "Import Ordering", albumMetadata?.ImportOrdering);
        Add(entries, "JSON Version", albumMetadata?.JsonCompatibilityVersion?.ToString());

        if (photo.RgbnData is not null)
        {
            Add(entries, "Composite Type", "RGB");
            Add(entries, "Composite Hash", photo.RgbnData.CompositeHash);
            Add(entries, "Blend Mode", photo.RgbnData.BlendMode);
        }

        if (photo.AverageData is not null)
        {
            Add(entries, "Composite Type", "Average");
            Add(entries, "Composite Hash", photo.AverageData.CompositeHash);
            Add(entries, "Average Algorithm", photo.AverageData.Algorithm);
            Add(entries, "Channel Order", photo.AverageData.ChannelOrder.ToString());
            Add(entries, "Average Groups", photo.AverageData.SourceGroups.Count.ToString());
            Add(entries, "Average Inputs", photo.AverageData.SourcePhotoCount.ToString());
        }

        GameBoyCameraFrameMetadata? metadata = photo.Metadata;
        if (metadata is null)
        {
            return entries;
        }

        Add(entries, "Album Index", metadata.AlbumIndex >= 0 ? metadata.AlbumIndex.ToString() : null);
        Add(entries, "Cart Index", metadata.CartIndex >= 0 ? metadata.CartIndex.ToString() : null);
        Add(entries, "Base Address", metadata.BaseAddress > 0 ? $"0x{metadata.BaseAddress:X}" : null);
        Add(entries, "Frame Number", metadata.FrameNumber.ToString());
        Add(entries, "User ID", metadata.UserId);
        Add(entries, "User Name", metadata.UserName);
        Add(entries, "Birth Date", metadata.BirthDate);
        Add(entries, "Gender", metadata.Gender);
        Add(entries, "Blood Type", metadata.BloodType);
        Add(entries, "Comment", metadata.Comment);
        Add(entries, "Is Copy", metadata.IsCopy ? "Yes" : "No");
        Add(entries, "ROM Type", metadata.RomType);
        Add(entries, "Exposure", metadata.Exposure);
        Add(entries, "Capture Mode", metadata.CaptureMode);
        Add(entries, "Edge Exclusive", metadata.EdgeExclusive);
        Add(entries, "Edge Operation", metadata.EdgeOperation);
        Add(entries, "Edge Mode", metadata.EdgeMode);
        Add(entries, "Gain", metadata.Gain);
        Add(entries, "Invert Output", metadata.InvertOutput);
        Add(entries, "Voltage Reference", metadata.VoltageReference);
        Add(entries, "Zero Point", metadata.ZeroPoint);
        Add(entries, "Voltage Output", metadata.VoltageOutput);
        Add(entries, "Dither Set", metadata.DitherSet);
        Add(entries, "Contrast", metadata.Contrast?.ToString());

        return entries;
    }

    private static void Add(ICollection<MetadataEntry> entries, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            entries.Add(new MetadataEntry(label, value));
        }
    }
}