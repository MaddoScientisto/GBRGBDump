using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace GBTools.Decoder
{
    public interface ICompressAndHashService
    {
        Task<HashedCompressed> CompressAndHash(List<string> lines);
    }

    public class CompressAndHashService : ICompressAndHashService
    {
        private readonly IApplyFrameService _applyFrameService;

        public CompressAndHashService(IApplyFrameService applyFrameService)
        {
            _applyFrameService = applyFrameService;
        }

        public async Task<HashedCompressed> CompressAndHash(List<string> lines)
        {
            var imageData = string.Join("\n", lines.Select(line => line.Replace(" ", "").ToUpper()));

            var compressed = Compress(imageData);
            var dataHash = Hash(compressed);

            return new HashedCompressed
            {
                DataHash = dataHash,
                Compressed = compressed
            };
        }

        private string Compress(string data)
        {
            // Implement compression logic
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(data)); // Placeholder
        }

        private string Decompress(string data)
        {
            // Implement decompression logic
            return Encoding.UTF8.GetString(Convert.FromBase64String(data)); // Placeholder
        }

        private string Hash(string data)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
                return BitConverter.ToString(hashedBytes).Replace("-", "").ToLowerInvariant();
            }
        }

        private List<string> DummyImage(string dataHash)
        {
            // Generate a dummy image based on the hash
            return new List<string> { "Dummy Image Data" }; // Placeholder
        }
    }

    public class HashedCompressed
    {
        public string DataHash { get; set; }
        public string Compressed { get; set; }
    }
    
}
