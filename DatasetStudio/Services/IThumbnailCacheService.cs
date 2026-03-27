using System.IO;
using System.Threading.Tasks;

namespace DatasetStudio.Services;

public interface IThumbnailCacheService
{
    Task<Stream> GetThumbnailAsync(string imageFilePath, int size);

    Task InvalidateAsync(string imageFilePath);

    Task InvalidateFolderAsync(string folderPath);
}
