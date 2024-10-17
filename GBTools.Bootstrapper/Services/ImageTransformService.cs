using System;
using System.Collections.Concurrent;
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

        public ImageTransformService(IImportSavService importSavService, IFileReaderService fileReaderService,
            IFileWriterService fileWriterService,
            IGameboyPrinterService gameboyPrinter /*IDecoderService decoderService*/)
        {
            _importSavService = importSavService;
            _fileReaderService = fileReaderService;
            _fileWriterService = fileWriterService;
            //_decoderService = decoderService;
            _gameboyPrinterService = gameboyPrinter;
        }

        public async Task<List<GbImageContainer>> TransformSavToBase64ArrayAsync(string filePath, ImportSavOptions options)
        {
            try
            {
                var (itemsToProcess, progressInfo) = await PreprocessSavFile(filePath, options);

                var exceptions = new ConcurrentQueue<Exception>();

                var results = new ConcurrentQueue<GbImageContainer>();

                await Parallel.ForEachAsync(itemsToProcess, new ParallelOptions()
                {
                    MaxDegreeOfParallelism = 8
                }, async (importParams, token) =>
                {
                    try
                    {
                        var importItems = await _importSavService.ImportSav(importParams);

                        foreach (var item in importItems)
                        {
                            var base64Image = await _gameboyPrinterService.RenderAndSaveToBase64Png(item.Tiles);
                            GbImageContainer container = new GbImageContainer()
                            {
                                Base64Png = base64Image,
                                Name = item.FileName,
                                ImageHash = item.ImageHash,
                                Id = item.Index,
                                Bank = item.Bank,
                            };

                            results.Enqueue(container);
                        }

                    }
                    catch (Exception e)
                    {
                        exceptions.Enqueue(e);
                        Debug.WriteLine(e);
                    }
                });

                if (!exceptions.IsEmpty)
                {
                    throw new AggregateException(exceptions);
                }

                progressInfo.CurrentImage--;

                //progress?.Report(progressInfo);

                var sortedList = results.OrderBy(x => x.Bank).ThenBy(x => x.Id).ToList();

                return sortedList;

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }



        }

        private async Task<(List<ImportSavParams> itemsToProcess, ProgressInfo progressInfo)> PreprocessSavFile(string filePath, ImportSavOptions options)
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
                int startChunkIndex =
                    data.Length > maxChunkSize ? 1 : 0; // Skip the first chunk only if the file is larger than 128KB

                progressInfo.TotalBanks = totalChunks;
                progressInfo.CurrentBank = 1;
                progressInfo.CurrentImage = 1;
                progressInfo.CurrentImageName = string.Empty;

                var itemsToProcess = new List<ImportSavParams>();

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

                    itemsToProcess.Add(importParams);
                }

                return (itemsToProcess, progressInfo);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public async Task<bool> TransformSav(string filePath, string outputPath, ImportSavOptions options,
            IProgress<ProgressInfo>? progress = null)
        {
            try
            {
                var (itemsToProcess, progressInfo) = await PreprocessSavFile(filePath, options);
                progress?.Report(progressInfo);
                //var progressInfo = new ProgressInfo();

                //var data = await _fileReaderService.ReadFileAsByteArray(filePath);
                //var lastModified = File.GetLastWriteTimeUtc(filePath);
                //var fileName = Path.GetFileName(filePath);
                //var filenameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
                //var extension = Path.GetExtension(filePath);

                //const int maxChunkSize = 128 * 1024; // 128KB
                //int totalChunks = (data.Length + maxChunkSize - 1) / maxChunkSize;
                //int startChunkIndex =
                //    data.Length > maxChunkSize ? 1 : 0; // Skip the first chunk only if the file is larger than 128KB

                //progressInfo.TotalBanks = totalChunks;
                //progressInfo.CurrentBank = 1;
                //progressInfo.CurrentImage = 1;
                //progressInfo.CurrentImageName = string.Empty;

                //progress?.Report(progressInfo);

                //var itemsToProcess = new List<ImportSavParams>();

                //for (int chunkIndex = startChunkIndex; chunkIndex < totalChunks; chunkIndex++)
                //{
                //    int offset = chunkIndex * maxChunkSize;
                //    int length = Math.Min(maxChunkSize, data.Length - offset);

                //    var chunkData = new byte[length];
                //    Array.Copy(data, offset, chunkData, 0, length);

                //    string formattedFileName = totalChunks > 1
                //        ? $"{filenameWithoutExtension}_BANK_{chunkIndex:D2}"
                //        : $"{filenameWithoutExtension}";

                //    progressInfo.CurrentImage = 1;
                //    progressInfo.CurrentImageName = formattedFileName;

                //    // Assuming default values for parameters
                //    var importParams = new ImportSavParams
                //    {
                //        Data = chunkData,
                //        LastModified = lastModified.Ticks,
                //        FileName = formattedFileName,
                //        Options = options,
                //        Bank = chunkIndex
                //    };

                //    itemsToProcess.Add(importParams);
                //}

                var exceptions = new ConcurrentQueue<Exception>();
                
                await Parallel.ForEachAsync(itemsToProcess, new ParallelOptions()
                {
                    MaxDegreeOfParallelism = 8
                }, async (importParams, token) =>
                {
                    try
                    {
                        var importItems = await _importSavService.ImportSav(importParams);

                        foreach (var item in importItems)
                        {
                            if (!Directory.Exists(outputPath))
                            {
                                Directory.CreateDirectory(outputPath);
                            }

                            progress?.Report(new ProgressInfo(){
                                CurrentBank = item.Bank,
                                CurrentImage = item.Index,
                                CurrentImageName = item.FileName,
                                TotalBanks = progressInfo.TotalBanks,
                                TotalImages = importItems.Count,
                            });

                            await _gameboyPrinterService.RenderAndSaveAsPng(item.Tiles,
                                Path.Combine(outputPath, $"{item.FileName}.png"));
                        }

                        if (!options.RgbMerge)
                        {
                            return;
                        }
                    
                        // RGB and HDR Merge
                        await _gameboyPrinterService.RenderAndHDRMerge(importItems, outputPath, options.AverageType,
                            options.ChannelOrder);
                    }
                    catch (Exception e)
                    {
                        exceptions.Enqueue(e);
                        Debug.WriteLine(e);
                    }
                });

                if (!exceptions.IsEmpty)
                {
                    throw new AggregateException(exceptions);
                }
                
                progressInfo.CurrentImage--;

                //progress?.Report(progressInfo);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing file: {ex.Message}");
                throw;
                //return false;
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