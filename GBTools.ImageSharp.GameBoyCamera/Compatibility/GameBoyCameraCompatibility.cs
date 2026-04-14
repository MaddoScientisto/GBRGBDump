using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace GBTools.ImageSharp.GameBoyCamera.Compatibility;

public static class GameBoyCameraCompatibility
{
    public static Image<Rgba32> LoadSaveDump(Stream stream, GameBoyCameraLoadOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        _ = options ?? new GameBoyCameraLoadOptions();

        throw new NotSupportedException("Save dump loading is not implemented yet. Port the gb-printer-web save traversal, metadata parsing, and frame application logic into the shared codec core first.");
    }

    public static Task ExportGbPrinterWebJsonAsync(Image image, Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(stream);

        throw new NotSupportedException("gb-printer-web JSON export is not implemented yet. Add the compatibility document model and binary payload mapper first.");
    }
}