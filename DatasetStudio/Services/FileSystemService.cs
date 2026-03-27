using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DatasetStudio.Services;

public class FileSystemService : IFileSystemService
{
    private static readonly HashSet<string> SupportedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".webp",
        ".bmp",
    };

    public Task<IReadOnlyList<string>> DiscoverProjectFoldersAsync(string masterRootPath)
    {
        if (!Directory.Exists(masterRootPath))
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        List<string> projectFolders = Directory
            .EnumerateDirectories(masterRootPath)
            .Where(directoryPath => File.Exists(Path.Combine(directoryPath, ".datasetstudio.json")))
            .OrderBy(directoryPath => directoryPath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult<IReadOnlyList<string>>(projectFolders);
    }

    public Task<IReadOnlyList<string>> GetImageFilesAsync(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        List<string> imageFiles = Directory
            .EnumerateFiles(folderPath)
            .Where(filePath => SupportedImageExtensions.Contains(Path.GetExtension(filePath)))
            .OrderBy(filePath => filePath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult<IReadOnlyList<string>>(imageFiles);
    }

    public Task MoveFileAsync(string sourcePath, string destinationFolder)
    {
        string fileName = Path.GetFileName(sourcePath);
        string destinationPath = Path.Combine(destinationFolder, fileName);

        if (File.Exists(destinationPath))
        {
            throw new IOException($"Destination file already exists: {destinationPath}");
        }

        Directory.CreateDirectory(destinationFolder);
        File.Move(sourcePath, destinationPath);
        return Task.CompletedTask;
    }

    public Task RecycleFileAsync(string filePath)
    {
        return Task.Run(() =>
        {
            FileSystem.DeleteFile(
                filePath,
                UIOption.OnlyErrorDialogs,
                RecycleOption.SendToRecycleBin);
        });
    }

    public Task EnsureFolderExistsAsync(string folderPath)
    {
        Directory.CreateDirectory(folderPath);
        return Task.CompletedTask;
    }

    public FileSystemWatcher WatchFolder(string folderPath)
    {
        Directory.CreateDirectory(folderPath);

        FileSystemWatcher fileSystemWatcher = new(folderPath)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.CreationTime
                | NotifyFilters.DirectoryName
                | NotifyFilters.FileName
                | NotifyFilters.LastWrite,
            EnableRaisingEvents = false,
        };

        return fileSystemWatcher;
    }
}
