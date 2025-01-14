namespace GBTools.Common.Services.Impl
{
    public class FileSystemService : IFileSystemService
    {
        public void CreateDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        public bool FileExists(string path)
        {
            return File.Exists(path);
        }

        public IEnumerable<string> GetFileSystemEntries(string path)
        {
            if (!Directory.Exists(path)) return [];

            return Directory.GetFileSystemEntries(path, "*", SearchOption.AllDirectories);
        }

        private async Task<string> ImageToRawBase64Async(string imagePath)
        {
            // Read the image file asynchronously into a byte array
            byte[] imageBytes = await File.ReadAllBytesAsync(imagePath);

            // Convert byte array to base64 string
            string base64String = Convert.ToBase64String(imageBytes);

            return base64String;
        }

        public async Task<string> ImageToBase64Async(string imagePath)
        {
            try
            {
                // Convert byte array to base64 string
                string base64String = await ImageToRawBase64Async(imagePath);

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

        public async Task<GbImageContainer> ImageToGbImageAsync(string imagePath)
        {
            string base64String = await ImageToRawBase64Async(imagePath);

            var img = new GbImageContainer()
            {
                Base64Png = base64String,
                Name = Path.GetFileNameWithoutExtension(imagePath),

            };

            return img;
        }

        public async Task WriteBase64ToFile(string base64Image, string folder, string fileName)
        {
            await File.WriteAllBytesAsync(Path.Combine(folder, fileName), Convert.FromBase64String(base64Image));
        }

        public string MakeOutputSubFolder(string source, string destination)
        {
            return System.IO.Path.Combine(destination, System.IO.Path.GetFileNameWithoutExtension(source));
        }
    }
}