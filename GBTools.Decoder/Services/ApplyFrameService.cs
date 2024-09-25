using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace GBTools.Decoder
{
    public interface IApplyFrameService
    {
        Task<List<string>> ApplyFrame(List<string> tiles, string frameHash);
    }

    public class ApplyFrameService : IApplyFrameService
    {
        private readonly IFrameDataService _frameDataService;

        public ApplyFrameService(IFrameDataService frameDataService)
        {
            _frameDataService = frameDataService;
        }

        public async Task<List<string>> ApplyFrame(List<string> tiles, string frameHash)
        {
            // Image must be "default" dimensions
            if (tiles.Count != 360)
            {
                return tiles;
            }

            var frameData = await _frameDataService.LoadFrameData(frameHash);

            if (frameData == null)
            {
                return tiles;
            }

            if (frameData.Left.Count != 14 || frameData.Right.Count != 14)
            {
                Console.Error.WriteLine("Invalid frameData");
                return tiles;
            }

            var unframedImage = tiles.Where((tile, index) => !TileIndexIsPartOfFrame(index, 2)).ToList();
            var result = new List<string>(frameData.Upper);

            for (int line = 0; line < 14; line++)
            {
                result.AddRange(frameData.Left[line]);
                result.AddRange(unframedImage.Skip(line * 16).Take(16));
                result.AddRange(frameData.Right[line]);
            }

            result.AddRange(frameData.Lower);

            return result;
        }

        private bool TileIndexIsPartOfFrame(int index, int frameWidth)
        {
            // Implement logic based on specific frame structure
            return false; // Placeholder
        }
    }

    public interface IFrameDataService
    {
        Task<FrameData> LoadFrameData(string frameHash);
    }

    public class FrameDataService : IFrameDataService
    {
        public FrameDataService()
        {

        }

        public async Task<(string dataHash, byte[] compressed)> CompressAndHashFrame(List<string> lines, int imageStartLine)
        {
            //var frameData = JsonConvert.SerializeObject(GetFrameFromFullTiles(lines, imageStartLine));
            //var compressed = Compress(frameData);
            //var dataHash = ComputeHash(compressed);
            //return (dataHash, compressed);
            return (null, null);
        }

        public async Task<string> SaveFrameData(List<string> lines, int imageStartLine)
        {
            //var (dataHash, compressed) = await CompressAndHashFrame(lines, imageStartLine);
            //await _localForageFrames.SetItemAsync(dataHash, compressed);
            //return dataHash;
            return null;
        }

        public async Task<FrameData> LoadFrameData(string frameHash)
        {
            if (string.IsNullOrEmpty(frameHash))
                return null;

            return null;
            //var binary = await _localForageFrames.GetItemAsync(frameHash);
            //if (binary == null)
            //    return null;

            //try
            //{
            //    var raw = Decompress(binary);
            //    return JsonConvert.DeserializeObject<FrameData>(raw);
            //}
            //catch
            //{
            //     Handle older format or errors
            //    return null;
            //}
        }

        private byte[] Compress(string data)
        {
            using (var output = new MemoryStream())
            {
                using (var compressor = new GZipStream(output, CompressionMode.Compress))
                {
                    using (var writer = new StreamWriter(compressor))
                    {
                        writer.Write(data);
                    }
                }
                return output.ToArray();
            }
        }

        private string Decompress(byte[] data)
        {
            using (var input = new MemoryStream(data))
            using (var decompressor = new GZipStream(input, CompressionMode.Decompress))
            using (var reader = new StreamReader(decompressor))
            {
                return reader.ReadToEnd();
            }
        }

        private string ComputeHash(byte[] data)
        {
            using (var sha256 = SHA256.Create())
            {
                return Convert.ToBase64String(sha256.ComputeHash(data));
            }
        }
    }

    public class FrameData
    {
        public List<string> Upper { get; set; }
        public List<List<string>> Left { get; set; }
        public List<List<string>> Right { get; set; }
        public List<string> Lower { get; set; }
    }
}
