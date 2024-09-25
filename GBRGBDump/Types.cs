using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GBRGBDump
{
    internal class Types
    {

    }

    public enum RomTypes
    {
        Photo,
        Stock,
        Pxlr
    }

    public class CustomMetaData
    {
        public RomTypes RomType { get; set; }
        public string Exposure { get; set; }
        public string CaptureMode { get; set; }
        public string EdgeExclusive { get; set; }
        public string EdgeOperation { get; set; }
        public string Gain { get; set; }
        public string EdgeMode { get; set; }
        public string InvertOut { get; set; }
        public string VoltageRef { get; set; }
        public string ZeroPoint { get; set; }
        public string VOut { get; set; }
        public string Ditherset { get; set; }
        public int? Contrast { get; set; }
    }

    public class BasicMetaData
    {
        public string UserId { get; set; }
        public string BirthDate { get; set; }
        public string UserName { get; set; }
        public string Gender { get; set; }
        public string BloodType { get; set; }
        public string Comment { get; set; }
        public bool IsCopy { get; set; }
    }

    public class ImportItem
    {
        public string FileName { get; set; }
        public string ImageHash { get; set; }
        public List<string> Tiles { get; set; }
        public long? LastModified { get; set; } // Using nullable long to represent optional number
        public string TempId { get; set; }
        public ImageMetaData Meta { get; set; } // Assuming ImageMetaData is already defined
    }

    public class ImageMetaData
    {
        public RomTypes RomType { get; set; }
        public CustomMetaData CustomMeta { get; set; }
        public BasicMetaData BasicMeta { get; set; }
    }

    public class FileMetaData
    {
        public int CartIndex { get; set; }
        public int AlbumIndex { get; set; }
        public int BaseAddress { get; set; }
        public int FrameNumber { get; set; }
        public ImageMetaData Meta { get; set; }
    }

    public class FileMetadataWithTiles
    {
        public FileMetaData Meta { get; set; }
        public List<string> Tiles { get; set; }
    }

    public class GenerateFilenameOptions
    {
        public string IndexText { get; set; }
        public int AlbumIndex { get; set; }
        public int DisplayIndex { get; set; }
    }

    public delegate string GenerateFilenameFn(GenerateFilenameOptions options);

    public delegate Task<bool> ImportSavFn(string selectedFrameset, bool cartIsJP);

    public class ImportSavParams
    {
        public bool ImportLastSeen { get; set; }
        public byte[] Data { get; set; }
        public long LastModified { get; set; }
        public List<Frame> Frames { get; set; }
        public object FileName { get; set; } // Can be string or GenerateFilenameFn
        public bool ImportDeleted { get; set; }
        public Action<AnyAction> Dispatch { get; set; }
        public bool ForceMagicCheck { get; set; }
    }

    public class Frame
    {
        // Define properties of Frame as needed
    }

    public class AnyAction
    {
        public string Id { get; set; }
        public string Hash { get; set; }
        public string Name { get; set; }
        // public string FileName { get; set; } // Uncomment if needed
        // public List<string> Tiles { get; set; } // Uncomment if needed
        // public string ImageHash { get; set; } // Uncomment if needed
        public string TempId { get; set; }
    }

}
