using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GBTools.Common;
using GBTools.Graphics.Services;

namespace GBRGBDump.WebShared.Services.Impl
{
    public class AutoRGBMergeService
    {
        private readonly ImageProcessingService _imageProcessingService;
        public AutoRGBMergeService(ImageProcessingService imageProcessingService)
        {
            _imageProcessingService = imageProcessingService;
        }

        public async Task<IList<GbImageContainer>> AutoRGBMerge(IList<GbImageContainer> images, ChannelOrder channelOrder, AverageTypes averageType, int aebStep)
        {
            List<GbImageContainer> createdImages = [];

            var elements = ((aebStep * 2) + 1) * 3;

            var imagseToMerge = images
                .Select((item, index) => new { item, index })
                .GroupBy(x => x.index / elements)
                .Select(g => g.Select(x => x.item).ToList())
                .ToList();

            var date = DateTime.Now;

            int groupIndex = 0;
            foreach (var group in imagseToMerge)
            {
                var mergedImages = await _imageProcessingService.RGBMergeAsync(group.Select(img => img.Base64Png).ToList(), channelOrder);

                int imgIndex = 0;

                var createdGroup = new List<GbImageContainer>();

                foreach (var mergedImage in mergedImages)
                {
                    var id = GetNewUniqueId(images, createdImages, createdGroup);

                    createdGroup.Add(new GbImageContainer()
                    {
                        Base64Png = mergedImage,
                        Id = id,
                        Name = $"{date.ToSortableFileName()} {id} {groupIndex} RGB {imgIndex}",
                        Tags = ["RGB"]
                    });
                    // Note: ids may be wrong
                    imgIndex++;
                }

                if (averageType is AverageTypes.Normal or AverageTypes.FullBank)
                {
                    var averagedImage = await _imageProcessingService.AverageAsync(createdGroup.Select(x => x.Base64Png).ToList());


                    var id = GetNewUniqueId(images, createdImages, createdGroup);
                    createdGroup.Add(new GbImageContainer()
                    {
                        Base64Png = averagedImage,
                        Id = id,
                        Name = $"{createdGroup.First().Name} HDR {id}",
                        Tags = ["RGB", "HDR"]
                    });
                }

                foreach (var image in createdGroup)
                {
                    createdImages.Add(image);
                }

                groupIndex++;
            }

            return createdImages;
        }

        public int GetNewUniqueId(params IList<GbImageContainer>[] lists)
        {
            int maxId = lists
                .SelectMany(list => list)
                .Select(container => container.Id)
                .DefaultIfEmpty(0)
                .Max();

            return maxId + 1;
        }
    }
}
