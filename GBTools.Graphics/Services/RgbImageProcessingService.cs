using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GBTools.Common;

namespace GBTools.Graphics
{

    public interface IRgbImageProcessingService
    {
        Task ProcessImages(string inputPath, string outputPath, ChannelOrder channelOrder,
            IProgress<ProgressInfo>? progress = null);

        Task ProcessImages(List<RenderedGameBoyImage> renderedImages, string outputPath, ChannelOrder channelOrder, AverageTypes averageType,
            IProgress<ProgressInfo>? progress = null);
    }

    public class RgbImageProcessingService : IRgbImageProcessingService
    {
        public RgbImageProcessingService()
        {

        }

        public async Task ProcessImages(string inputPath, string outputPath, ChannelOrder channelOrder, IProgress<ProgressInfo>? progress = null)
        {
            var imageFiles = Directory.GetFiles(inputPath, "*.png");
            var groupedImages = GroupImagesByBankAndNumber(imageFiles);

            var progressInfo = new ProgressInfo();
            progressInfo.CurrentImage = 1;
            progressInfo.CurrentBank = 1;
            progressInfo.CurrentImageName = string.Empty;
            progressInfo.TotalBanks = groupedImages.Count + 1;
            progressInfo.TotalImages = 0;

            progress?.Report(progressInfo);

            foreach (var bank in groupedImages)
            {
                for (int groupIndex = 0; groupIndex < bank.Value.Count; groupIndex++)
                {
                    progress?.Report(progressInfo);

                    var group = bank.Value[groupIndex];
                    var mergedImages = MergeGroupImages(group, channelOrder);
                    await SaveMergedImages(outputPath, mergedImages, bank.Key, groupIndex + 1);

                    progressInfo.CurrentImage++;
                }

                progress?.Report(progressInfo);
                progressInfo.CurrentBank++;
            }

            progressInfo.CurrentImage--;
            progress?.Report(progressInfo);
        }

        public async Task ProcessImages(List<RenderedGameBoyImage> renderedImages, string outputPath, ChannelOrder channelOrder, AverageTypes averageType,
            IProgress<ProgressInfo>? progress = null)
        {
            var averageOutputFolder = Path.Combine(outputPath, "average");
            if (!Directory.Exists(averageOutputFolder))
            {
                Directory.CreateDirectory(averageOutputFolder);
                Console.WriteLine($"Created the directory: {averageOutputFolder}");
            }

            // Just split list in two parts
            // Very naive for now
            var groups = new List<List<RenderedGameBoyImage>>();
            groups.Add([..renderedImages.Take(15)]);
            groups.Add([..renderedImages.TakeLast(15)]);

            var allMerged = new List<SKBitmap>();

            int gi = 1;
            foreach (var group in groups)
            {
                var merged = MergeGroupImages(group, ChannelOrder.Sequential);
                if (averageType == AverageTypes.FullBank)
                {
                    allMerged.AddRange(merged);
                }

                if (averageType != AverageTypes.None)
                {
                    // Create averages
                    var (averagedImage, scaledAveragedImage) = CreateAveragedImage(merged);

                    var filename = Path.GetFileNameWithoutExtension(group.First().RawData.FileName);
                    var bank = group.First().RawData.Bank;

                    var fn = Path.GetFileName($"{filename} {bank:00}");

                    await SaveMergedImages(outputPath, merged, fn, gi);

                    // Save average
                    await SaveMergedImages(averageOutputFolder, [averagedImage, scaledAveragedImage], $"{fn} average", gi++);
                    
                    // Done, dispose
                    averagedImage.Dispose();
                    scaledAveragedImage.Dispose();
                }

                if (averageType != AverageTypes.FullBank)
                {
                    foreach (var bitmap in merged)
                    {
                        bitmap.Dispose();
                    }
                }
            }

            if (averageType == AverageTypes.FullBank)
            {
                //Do double group HDR
                var fullMergedFilename = $"{Path.GetFileNameWithoutExtension(renderedImages.First().RawData.FileName)} full" ;
                var fullMergedBank = renderedImages.First().RawData.Bank;
            
                // Create averages
                var (fullAveragedImage, fullScaledAveragedImage) = CreateAveragedImage(allMerged);
            
                await SaveMergedImages(averageOutputFolder, new List<SKBitmap>([fullAveragedImage, fullScaledAveragedImage]), $"{fullMergedFilename} {fullMergedBank:00} fullaverage", gi++);

                foreach (var bitmap in allMerged)
                {
                    bitmap.Dispose();
                }
            } 
        }

        private List<SKBitmap> MergeGroupImages(List<RenderedGameBoyImage> imageGroup, ChannelOrder order)
        {
            var mergedImages = new List<SKBitmap>();
            List<SKBitmap> redImages, greenImages, blueImages;

            switch (order)
            {
                case ChannelOrder.Sequential:
                    redImages = imageGroup.Take(5).Select(x => SKBitmap.FromImage(x.RenderedImage)).ToList();
                    greenImages = imageGroup.Skip(5).Take(5).Select(x => SKBitmap.FromImage(x.RenderedImage)).ToList();
                    blueImages = imageGroup.Skip(10).Take(5).Select(x => SKBitmap.FromImage(x.RenderedImage)).ToList();
                    break;
                case ChannelOrder.Interleaved:
                    redImages = imageGroup.Where((_, index) => index % 3 == 0).Select(x => SKBitmap.FromImage(x.RenderedImage)).ToList();
                    greenImages = imageGroup.Where((_, index) => index % 3 == 1).Select(x => SKBitmap.FromImage(x.RenderedImage)).ToList();
                    blueImages = imageGroup.Where((_, index) => index % 3 == 2).Select(x => SKBitmap.FromImage(x.RenderedImage)).ToList();
                    break;
                default:
                    throw new ArgumentException("Unsupported channel order");
            }

            // Merge and store each RGB image
            for (int i = 0; i < redImages.Count; i++)
            {
                var mergedImage = MergeColorChannels(redImages[i], greenImages[i], blueImages[i]);
                mergedImages.Add(mergedImage);
            }

            // Create averaged image from the merged RGB images
            //var (averagedImage, scaledAveragedImage) = CreateAveragedImage(mergedImages);
            //mergedImages.Add(averagedImage);
            //mergedImages.Add(scaledAveragedImage);

            return mergedImages;
        }

        private Dictionary<string, List<List<string>>> GroupImagesByBankAndNumber(string[] imageFiles)
        {
            var regex = new Regex(@"(.*?)(_BANK_(\d{2}))? (\d{2})( \[deleted\])?\.png");
            var groups = new Dictionary<string, List<List<string>>>();

            foreach (var file in imageFiles)
            {
                var match = regex.Match(file);
                if (match.Success)
                {
                    var prefix = match.Groups[1].Value;
                    var bank = match.Groups[2].Value;
                    var number = int.Parse(match.Groups[3].Value);

                    var bankKey = $"{prefix}_BANK_{bank}";
                    if (!groups.ContainsKey(bankKey))
                    {
                        groups[bankKey] = new List<List<string>> { new List<string>(), new List<string>() };
                    }

                    if (number <= 15)
                        groups[bankKey][0].Add(file);
                    else
                        groups[bankKey][1].Add(file);
                }
            }

            return groups;
        }

        //private List<SKBitmap> MergeGroupImages(List<string> imageGroup)
        //{
        //    var mergedImages = new List<SKBitmap>();
        //    // Assuming groups are correctly formed and have exactly 15 images
        //    for (int i = 0; i < 5; i++)
        //    {
        //        var redImages = imageGroup.Where((_, index) => index % 5 == i).Select(img => SKBitmap.FromImage(SKImage.FromEncodedData(img))).ToList();
        //        var greenImages = imageGroup.Where((_, index) => index % 5 == (i + 1) % 5).Select(img => SKBitmap.FromImage(SKImage.FromEncodedData(img))).ToList();
        //        var blueImages = imageGroup.Where((_, index) => index % 5 == (i + 2) % 5).Select(img => SKBitmap.FromImage(SKImage.FromEncodedData(img))).ToList();

        //        mergedImages.Add(MergeColorChannels(redImages, greenImages, blueImages));
        //    }

        //    return mergedImages;
        //}

        private List<SKBitmap> MergeGroupImages(List<string> imageGroup, ChannelOrder order)
        {
            var mergedImages = new List<SKBitmap>();
            List<SKBitmap> redImages, greenImages, blueImages;

            switch (order)
            {
                case ChannelOrder.Sequential:
                    redImages = imageGroup.Take(5).Select(SKBitmap.Decode).ToList();
                    greenImages = imageGroup.Skip(5).Take(5).Select(SKBitmap.Decode).ToList();
                    blueImages = imageGroup.Skip(10).Take(5).Select(SKBitmap.Decode).ToList();
                    break;
                case ChannelOrder.Interleaved:
                    redImages = imageGroup.Where((_, index) => index % 3 == 0).Select(SKBitmap.Decode).ToList();
                    greenImages = imageGroup.Where((_, index) => index % 3 == 1).Select(SKBitmap.Decode).ToList();
                    blueImages = imageGroup.Where((_, index) => index % 3 == 2).Select(SKBitmap.Decode).ToList();
                    break;
                default:
                    throw new ArgumentException("Unsupported channel order");
            }

            // Merge and store each RGB image
            for (int i = 0; i < redImages.Count; i++)
            {
                var mergedImage = MergeColorChannels(redImages[i], greenImages[i], blueImages[i]);
                mergedImages.Add(mergedImage);
            }

            // Create averaged image from the merged RGB images
            var (averagedImage, scaledAveragedImage) = CreateAveragedImage(mergedImages);
            mergedImages.Add(averagedImage);
            mergedImages.Add(scaledAveragedImage);

            //// There should be exactly 5 images per color channel
            //for (int i = 0; i < redImages.Count; i++)
            //{
            //    var redImage = redImages[i];
            //    var greenImage = greenImages[i];
            //    var blueImage = blueImages[i];

            //    var mergedImage = MergeColorChannels(redImage, greenImage, blueImage);
            //    mergedImages.Add(mergedImage);
            //}

            return mergedImages;
        }

        private SKBitmap MergeColorChannels(SKBitmap redImage, SKBitmap greenImage, SKBitmap blueImage)
        {
            int width = redImage.Width;
            int height = redImage.Height;

            var mergedBitmap = new SKBitmap(width, height);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var redPixel = redImage.GetPixel(x, y);
                    var greenPixel = greenImage.GetPixel(x, y);
                    var bluePixel = blueImage.GetPixel(x, y);

                    // Create a new color by combining the R, G, B components from each image
                    var mergedColor = new SKColor(redPixel.Red, greenPixel.Green, bluePixel.Blue);
                    mergedBitmap.SetPixel(x, y, mergedColor);
                }
            }

            return mergedBitmap;
        }

        private (SKBitmap, SKBitmap) CreateAveragedImage(List<SKBitmap> mergedImages)
        {
            int width = mergedImages[0].Width;
            int height = mergedImages[0].Height;

            var averagedBitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);

            using (var canvas = new SKCanvas(averagedBitmap))
            {
                canvas.Clear(SKColors.Transparent);

                for (int i = 0; i < mergedImages.Count; i++)
                {
                    float alpha = 1f / (i + 1);
                    using var paint = new SKPaint();
                    paint.Color = SKColors.White.WithAlpha((byte)(255 * alpha));
                    canvas.DrawBitmap(mergedImages[i], new SKPoint(0, 0), paint);
                }
            }

            // Create a 4x scaled version of the averaged image
            var scale = 4;
            var scaledWidth = width * scale;
            var scaledHeight = height * scale;
            var scaledBitmap = new SKBitmap(scaledWidth, scaledHeight, SKColorType.Rgba8888, SKAlphaType.Premul);

            using (var canvas = new SKCanvas(scaledBitmap))
            {
                canvas.Clear(SKColors.Transparent);
                using (var paint = new SKPaint())
                {
                    paint.IsAntialias = false;
                    paint.FilterQuality = SKFilterQuality.None;
                    canvas.DrawBitmap(averagedBitmap, new SKRect(0, 0, scaledWidth, scaledHeight), paint);
                }
            }

            return (averagedBitmap, scaledBitmap);
        }

        private async Task SaveMergedImages(string outputPath, List<SKBitmap> mergedImages, string baseFileName, int groupIndex)
        {
            string fileName = Path.GetFileName(baseFileName);
            for (int i = 0; i < mergedImages.Count; i++)
            {
                var path = Path.Combine(outputPath, $"{fileName} RGB {groupIndex:00} {i + 1:00}.png");
                await SaveImageAsync(mergedImages[i], path);
            }

            //var averageOutputFolder = Path.Combine(outputPath, "average");

            //if (!Directory.Exists(averageOutputFolder))
            //{
            //    Directory.CreateDirectory(averageOutputFolder);
            //    Console.WriteLine($"Created the directory: {averageOutputFolder}");
            //}

            //var averagePath = Path.Combine(averageOutputFolder, $"{fileName} RGB {groupIndex:00} average 1X.png");
            //await SaveImageAsync(mergedImages[mergedImages.Count - 2], averagePath);

            //var scaledAveragePath = Path.Combine(averageOutputFolder, $"{fileName} RGB {groupIndex:00} average 4X.png");
            //await SaveImageAsync(mergedImages.Last(), scaledAveragePath);
        }

        private async Task SaveImageAsync(SKBitmap image, string path)
        {
            using var skImage = SKImage.FromBitmap(image);
            using var data = skImage.Encode(SKEncodedImageFormat.Png, 100);
            await using var stream = File.OpenWrite(path);
            
            data.SaveTo(stream);
        }
    }
}
