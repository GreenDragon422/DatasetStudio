using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace DatasetStudio.Services;

public interface IFileSystemService
{
    Task<IReadOnlyList<string>> DiscoverProjectFoldersAsync(string masterRootPath);

    Task<IReadOnlyList<string>> GetImageFilesAsync(string folderPath);

    Task MoveFileAsync(string sourcePath, string destinationFolder);

    Task RecycleFileAsync(string filePath);

    Task EnsureFolderExistsAsync(string folderPath);

    FileSystemWatcher WatchFolder(string folderPath);
}
