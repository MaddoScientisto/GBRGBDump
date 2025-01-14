namespace GBTools.Common.Services;

public interface IFileSystemService
{
    void CreateDirectory(string path);
    bool FileExists(string path);

    IEnumerable<string> GetFileSystemEntries(string path);
    Task<string> ImageToBase64Async(string imagePath);
    Task<GbImageContainer> ImageToGbImageAsync(string imagePath);
    string MakeOutputSubFolder(string source, string destination);

    Task WriteBase64ToFile(string base64Image, string folder, string fileName);
}