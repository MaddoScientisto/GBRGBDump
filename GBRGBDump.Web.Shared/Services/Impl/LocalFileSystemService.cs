using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GBRGBDump.Web.Shared.Services.Impl
{
    public class LocalFileSystemService : IFileSystemService
    {
        public IEnumerable<string> GetFileSystemEntries(string path)
        {
            if (!Directory.Exists(path)) return [];

            return Directory.GetFileSystemEntries(path, "*", SearchOption.AllDirectories);
        }

        public async Task<string> ImageToBase64Async(string imagePath)
        {
            try
            {
                // Read the image file asynchronously into a byte array
                byte[] imageBytes = await File.ReadAllBytesAsync(imagePath);

                // Convert byte array to base64 string
                string base64String = Convert.ToBase64String(imageBytes);

                // Determine the file extension to create the correct data URL
                string extension = Path.GetExtension(imagePath).ToLower();

                // Map the file extension to the appropriate MIME type
                string mimeType = extension switch
                {
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    ".gif" => "image/gif",
                    ".bmp" => "image/bmp",
                    ".svg" => "image/svg+xml",
                    _ => throw new ArgumentException("Unsupported image format")
                };

                // Return the base64 string in the format required for HTML img tags
                return $"data:{mimeType};base64,{base64String}";
            }
            catch (Exception ex)
            {
                // Handle any errors
                Console.WriteLine($"Error converting image to base64: {ex.Message}");
                return null;
            }
        }

        public async Task WriteBase64ToFile(string base64Image, string folder, string fileName) 
        {
            await File.WriteAllBytesAsync(Path.Combine(folder, fileName), Convert.FromBase64String(base64Image));
        }

        public string MakeOutputSubFolder(string source, string destination)
        {
            return System.IO.Path.Combine(destination, System.IO.Path.GetFileNameWithoutExtension(source));
        }

        public void CreateDirectory(string path)
        {
            if (!Path.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }
    }
}
