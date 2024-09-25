using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GBRGBDump
{
    public class ImageTransformService
    {
        private readonly IImportSavService _importSavService;
        private readonly IFileReaderService _fileReaderService;
        private readonly IFileWriterService _fileWriterService;

        public ImageTransformService(IImportSavService importSavService, IFileReaderService fileReaderService, IFileWriterService fileWriterService)
        {
            _importSavService = importSavService;
            _fileReaderService = fileReaderService;
            _fileWriterService = fileWriterService;
        }

        public async Task<bool> TransformSav(string filePath, string outputPath)
        {
            try
            {
                byte[] data = await _fileReaderService.ReadFileAsByteArray(filePath);
                var lastModified = File.GetLastWriteTimeUtc(filePath);
                var fileName = Path.GetFileName(filePath);

                // Assuming default values for parameters
                var importParams = new ImportSavParams
                {
                    Data = data,
                    LastModified = lastModified.Ticks,
                    FileName = fileName,
                    ImportLastSeen = true,
                    ImportDeleted = true,
                    ForceMagicCheck = false
                };

                var importItems = await _importSavService.ImportSav(importParams, "", false);

                // Save the results to a local file system folder
                foreach (var item in importItems)
                {
                    string outputFilePath = Path.Combine(outputPath, item.FileName + ".txt"); // Assuming saving as text files
                    await _fileWriterService.WriteToFile(outputFilePath, item.Tiles);
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing file: {ex.Message}");
                return false;
            }
        }
    }

    public interface IFileReaderService
    {
        Task<byte[]> ReadFileAsByteArray(string filePath);
    }

    public interface IFileWriterService
    {
        Task WriteToFile(string filePath, List<string> content);
    }

    public class FileReaderService : IFileReaderService
    {
        public async Task<byte[]> ReadFileAsByteArray(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("The file was not found.", filePath);
            }

            return await File.ReadAllBytesAsync(filePath);
        }
    }

    public class FileWriterService : IFileWriterService
    {
        public async Task WriteToFile(string filePath, List<string> content)
        {
            string directory = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllLinesAsync(filePath, content);
        }
    }
}
