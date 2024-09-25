using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace GBRGBDump
{
    public interface ICompressAndHashService
    {
        Task<HashedCompressed> CompressAndHash(List<string> lines);
        Task<string> Save(List<string> lines);

        Task<List<string>> Load(string dataHash, string frameHash = null, bool noDummy = false,
            Action<string> recover = null);

        Task Delete(string dataHash);
        Task DeleteFrame(string dataHash);
    }

    public class CompressAndHashService : ICompressAndHashService
    {
        private readonly ILocalStorageService _localStorageService;
        private readonly IApplyFrameService _applyFrameService;

        public CompressAndHashService(ILocalStorageService localStorageService, IApplyFrameService applyFrameService)
        {
            _localStorageService = localStorageService;
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

        public async Task<string> Save(List<string> lines)
        {
            var result = await CompressAndHash(lines);
            await _localStorageService.SetItem(result.DataHash, result.Compressed);
            return result.DataHash;
        }

        public async Task<List<string>> Load(string dataHash, string frameHash = null, bool noDummy = false,
            Action<string> recover = null)
        {
            if (string.IsNullOrEmpty(dataHash))
            {
                return null;
            }

            try
            {
                var binary = await _localStorageService.GetItem(dataHash);

                if (binary == null)
                {
                    throw new Exception("Missing image data");
                }

                var inflated = Decompress(binary);
                var tiles = inflated.Split('\n').ToList();

                if (string.IsNullOrEmpty(frameHash))
                {
                    return tiles;
                }

                return await _applyFrameService.ApplyFrame(tiles, frameHash);
            }
            catch (Exception ex)
            {
                recover?.Invoke(dataHash);
                return noDummy ? new List<string>() : DummyImage(dataHash);
            }
        }

        public async Task Delete(string dataHash)
        {
            await _localStorageService.RemoveItem(dataHash);
        }

        public async Task DeleteFrame(string dataHash)
        {
            await _localStorageService.RemoveItem(dataHash); // Assuming frame data is stored similarly
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

    public interface ILocalStorageService
    {
        Task SetItem(string key, string data);
        Task<string> GetItem(string key);
        Task RemoveItem(string key);
    }

    public class LocalStorageService : ILocalStorageService
    {
        public Task SetItem(string key, string data)
        {
            throw new NotImplementedException();
        }

        public Task<string> GetItem(string key)
        {
            throw new NotImplementedException();
        }

        public Task RemoveItem(string key)
        {
            throw new NotImplementedException();
        }
    }
}
