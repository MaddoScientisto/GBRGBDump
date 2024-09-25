using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GBRGBDump
{
    public interface IImportSavService
    {
        Task<List<ImportItem>> ImportSav(ImportSavParams parameters, string selectedFrameset, bool cartIsJP);
    }

    public class ImportSavService : IImportSavService
    {
        private readonly IFileMetaService _fileMetaService;
        private readonly ITransformImageService _transformImageService;
        private readonly IApplyFrameService _applyFrameService;
        private readonly ICompressAndHashService _compressAndHashService;
        private readonly IRandomIdService _randomIdService;
        //private readonly IEventDispatcher _eventDispatcher;

        public ImportSavService(
            IFileMetaService fileMetaService,
            ITransformImageService transformImageService,
            IApplyFrameService applyFrameService,
            ICompressAndHashService compressAndHashService,
            IRandomIdService randomIdService
            /*IEventDispatcher eventDispatcher*/)
        {
            _fileMetaService = fileMetaService;
            _transformImageService = transformImageService;
            _applyFrameService = applyFrameService;
            _compressAndHashService = compressAndHashService;
            _randomIdService = randomIdService;
           // _eventDispatcher = eventDispatcher;
        }

        public async Task<List<ImportItem>> ImportSav(ImportSavParams parameters, string selectedFrameset, bool cartIsJP)
        {
            if (parameters.ForceMagicCheck && !IsMagic(parameters.Data))
            {
                return null;
            }

            var addresses = Enumerable.Range(2, 30).Select(index => index * 0x1000).Where(address => address < parameters.Data.Length).ToList();
            if (parameters.ImportLastSeen)
            {
                addresses.Insert(0, 0);
            }

            var images = await Task.WhenAll(addresses.Select<int, Task<FileMetadataWithTiles>>(async address =>
            {
                var meta = _fileMetaService.GetFileMeta(parameters.Data, address, cartIsJP);
                var transformedData = _transformImageService.TransformImage(parameters.Data, address);

                if (transformedData != null)
                {
                    var tiles = await _applyFrameService.ApplyFrame(transformedData, MapCartFrameToHash(meta.FrameNumber, selectedFrameset, parameters.Frames));
                    return new FileMetadataWithTiles { Tiles = tiles, Meta = meta };
                }

                return null;
            }));

            var sortedImages = images.Where(image => image != null).OrderBy(image => image.Meta.AlbumIndex).ToList();

            int displayIndex = 0;
            var imageData = await Task.WhenAll(sortedImages.Select<FileMetadataWithTiles, Task<ImportItem>>(async image =>
            {
                string indexText = GetIndexText(image.Meta.AlbumIndex, ref displayIndex, parameters.ImportDeleted);
                if (indexText == null)
                {
                    return null;
                }

                var imageHash = await _compressAndHashService.CompressAndHash(image.Tiles);

                // this is kind of useless, I'm not passing the function
                var fileName = parameters.FileName switch
                {
                    GenerateFilenameFn filenameFn => filenameFn(new GenerateFilenameOptions
                    {
                        IndexText = indexText,
                        AlbumIndex = image.Meta.AlbumIndex,
                        DisplayIndex = displayIndex
                    }),
                    string staticFileName => $"{staticFileName} {indexText}",
                    _ => throw new InvalidOperationException("FileName must be either a GenerateFilenameFn delegate or a string.")
                };

                //var fileName = parameters.FileName is GenerateFilenameFn filenameFn ? filenameFn(new GenerateFilenameOptions(){IndexText = indexText, AlbumIndex = image.Meta.AlbumIndex, DisplayIndex = displayIndex}) : $"{index}" parameters.FileName(indexText, image.Meta.AlbumIndex, displayIndex);

                return new ImportItem
                {
                    FileName = fileName,
                    ImageHash = imageHash.DataHash,
                    Tiles = image.Tiles,
                    LastModified = parameters.LastModified,
                    Meta = image.Meta.Meta,
                    TempId = _randomIdService.GenerateRandomId()
                };
            }));


            return imageData.ToList();
            //_eventDispatcher.Dispatch(new ImportQueueAddMultiAction
            //{
            //    Type = "IMPORTQUEUE_ADD_MULTI",
            //    Payload = imageData.Where(item => item != null).ToList()
            //});

            //return true;
        }

        private bool IsMagic(byte[] data)
        {
            var magicPlaces = new[] { 0x10D2, 0x11AB, 0x11D0, 0x11F5 };
            return magicPlaces.All(index => Encoding.ASCII.GetString(data, index, 5) == "Magic");
        }

        private string GetIndexText(int albumIndex, ref int displayIndex, bool importDeleted)
        {
            switch (albumIndex)
            {
                case 64:
                    return "[last seen]";
                case 255:
                    if (!importDeleted)
                    {
                        return null;
                    }
                    displayIndex++;
                    return $"{displayIndex:D2} [deleted]";
                default:
                    displayIndex++;
                    return displayIndex.ToString("D2");
            }
        }

        private string MapCartFrameToHash(int frameNumber, string selectedFrameset, List<Frame> frames)
        {
            // Implement mapping logic
            return "frameHash"; // Placeholder
        }
    }

    public interface IEventDispatcher
    {
        void Dispatch<TAction>(TAction action);
    }

    public class ImportQueueAddMultiAction
    {
        public string Type { get; set; }
        public List<ImportItem> Payload { get; set; }
    }
}