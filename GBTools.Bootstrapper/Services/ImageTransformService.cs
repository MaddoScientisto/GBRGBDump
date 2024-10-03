﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GBTools.Common;
using GBTools.Decoder;
using GBTools.Graphics;

namespace GBTools.Bootstrapper
{
    public class ImageTransformService
    {
        private readonly IImportSavService _importSavService;
        private readonly IFileReaderService _fileReaderService;
        private readonly IFileWriterService _fileWriterService;
        //private readonly IDecoderService _decoderService;
        private readonly IGameboyPrinterService _gameboyPrinterService;

        public ImageTransformService(IImportSavService importSavService, IFileReaderService fileReaderService, IFileWriterService fileWriterService, IGameboyPrinterService gameboyPrinter /*IDecoderService decoderService*/)
        {
            _importSavService = importSavService;
            _fileReaderService = fileReaderService;
            _fileWriterService = fileWriterService;
            //_decoderService = decoderService;
            _gameboyPrinterService = gameboyPrinter;
        }

        public async Task<bool> TransformSav(string filePath, string outputPath, ImportSavOptions options, IProgress<ProgressInfo>? progress = null)
        {
            try
            {
                var progressInfo = new ProgressInfo();

                var data = await _fileReaderService.ReadFileAsByteArray(filePath);
                var lastModified = File.GetLastWriteTimeUtc(filePath);
                var fileName = Path.GetFileName(filePath);
                var filenameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
                var extension = Path.GetExtension(filePath);

                const int maxChunkSize = 128 * 1024; // 128KB
                int totalChunks = (data.Length + maxChunkSize - 1) / maxChunkSize;
                int startChunkIndex = data.Length > maxChunkSize ? 1 : 0; // Skip the first chunk only if the file is larger than 128KB

                progressInfo.TotalBanks = totalChunks;
                progressInfo.CurrentBank = 1;
                progressInfo.CurrentImage = 1;
                progressInfo.CurrentImageName = string.Empty;

                progress?.Report(progressInfo);

                for (int chunkIndex = startChunkIndex; chunkIndex < totalChunks; chunkIndex++)
                {
                    int offset = chunkIndex * maxChunkSize;
                    int length = Math.Min(maxChunkSize, data.Length - offset);

                    var chunkData = new byte[length];
                    Array.Copy(data, offset, chunkData, 0, length);

                    string formattedFileName = totalChunks > 1
                        ? $"{filenameWithoutExtension}_BANK_{chunkIndex:D2}"
                        : $"{filenameWithoutExtension}";

                    progressInfo.CurrentImage = 1;
                    progressInfo.CurrentImageName = formattedFileName;

                    // Assuming default values for parameters
                    var importParams = new ImportSavParams
                    {
                        Data = chunkData,
                        LastModified = lastModified.Ticks,
                        FileName = formattedFileName,
                        Options = options,
                        Bank = chunkIndex
                    };

                    var importItems = await _importSavService.ImportSav(importParams);

                    progressInfo.TotalImages = importItems.Count;

                    // Save the results to a local file system folder
                    foreach (var item in importItems)
                    {
                        progress?.Report(progressInfo);
                        

                        //string formattedFileName = totalChunks > 1
                        //    ? $"{item.FileName}_BANK_{chunkIndex:D2}"
                        //    : $"{item.FileName}";

                        //string outputFilePath = Path.Combine(outputPath, $"{formattedFileName}.txt"); // Assuming saving as text files
                        //await _fileWriterService.WriteToFile(outputFilePath, item.Tiles);
                        if (!Directory.Exists(outputPath))
                        {
                            Directory.CreateDirectory(outputPath);
                        }

                        await _gameboyPrinterService.RenderAndSaveAsPng(item.Tiles,
                            Path.Combine(outputPath, $"{item.FileName}.png"));

                        progressInfo.CurrentImage++;
                    }

                    GC.Collect();
                    
                    if (options.AverageType != AverageTypes.None)
                    {
                        // HDR Merge
                        await _gameboyPrinterService.RenderAndHDRMerge(importItems, outputPath);
                    }
                    
                    GC.Collect();
                    
                    progress?.Report(progressInfo);

                    progressInfo.CurrentBank++;
                }

                progressInfo.CurrentImage--;

                progress?.Report(progressInfo);

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
