using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using System;
using System.IO;
using System.Threading.Tasks;

namespace DatasetStudio.Services;

public sealed class ThumbnailCacheService : IThumbnailCacheService
{
    private const string CacheFolderName = ".datasetstudio-cache";
    private const int DefaultWebPQuality = 90;

    public Task<Stream> GetThumbnailAsync(string imageFilePath, int size)
    {
        if (string.IsNullOrWhiteSpace(imageFilePath))
        {
            throw new ArgumentException("Image file path is required.", nameof(imageFilePath));
        }

        if (size <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), "Thumbnail size must be greater than zero.");
        }

        string thumbnailPath = GetThumbnailPath(imageFilePath, size);

        if (!IsCacheCurrent(imageFilePath, thumbnailPath))
        {
            GenerateThumbnail(imageFilePath, thumbnailPath, size);
        }

        Stream thumbnailStream = File.OpenRead(thumbnailPath);
        return Task.FromResult(thumbnailStream);
    }

    public Task InvalidateAsync(string imageFilePath)
    {
        if (string.IsNullOrWhiteSpace(imageFilePath))
        {
            throw new ArgumentException("Image file path is required.", nameof(imageFilePath));
        }

        string thumbnailFolderPath = GetCacheFolderPathForImage(imageFilePath);

        if (!Directory.Exists(thumbnailFolderPath))
        {
            return Task.CompletedTask;
        }

        string thumbnailFilePattern = $"{Path.GetFileNameWithoutExtension(imageFilePath)}_*.webp";

        foreach (string thumbnailPath in Directory.EnumerateFiles(thumbnailFolderPath, thumbnailFilePattern))
        {
            File.Delete(thumbnailPath);
        }

        return Task.CompletedTask;
    }

    public Task InvalidateFolderAsync(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            throw new ArgumentException("Folder path is required.", nameof(folderPath));
        }

        string cacheFolderPath = GetCacheFolderPathForFolder(folderPath);

        if (Directory.Exists(cacheFolderPath))
        {
            Directory.Delete(cacheFolderPath, true);
        }

        return Task.CompletedTask;
    }

    private static void GenerateThumbnail(string imageFilePath, string thumbnailPath, int size)
    {
        string? thumbnailDirectoryPath = Path.GetDirectoryName(thumbnailPath);

        if (!string.IsNullOrWhiteSpace(thumbnailDirectoryPath))
        {
            Directory.CreateDirectory(thumbnailDirectoryPath);
        }

        using Image image = Image.Load(imageFilePath);
        Rectangle cropRectangle = CalculateCenteredSquareCrop(image.Width, image.Height);

        image.Mutate(imageProcessingContext =>
        {
            imageProcessingContext.Crop(cropRectangle);
            imageProcessingContext.Resize(size, size);
        });

        WebpEncoder encoder = new()
        {
            Quality = DefaultWebPQuality,
            FileFormat = WebpFileFormatType.Lossy,
        };

        image.Save(thumbnailPath, encoder);
    }

    private static Rectangle CalculateCenteredSquareCrop(int sourceWidth, int sourceHeight)
    {
        int cropSize = Math.Min(sourceWidth, sourceHeight);
        int x = (sourceWidth - cropSize) / 2;
        int y = (sourceHeight - cropSize) / 2;

        return new Rectangle(x, y, cropSize, cropSize);
    }

    private static bool IsCacheCurrent(string imageFilePath, string thumbnailPath)
    {
        if (!File.Exists(thumbnailPath))
        {
            return false;
        }

        DateTime imageLastWriteTimeUtc = File.GetLastWriteTimeUtc(imageFilePath);
        DateTime thumbnailLastWriteTimeUtc = File.GetLastWriteTimeUtc(thumbnailPath);
        return thumbnailLastWriteTimeUtc >= imageLastWriteTimeUtc;
    }

    private static string GetThumbnailPath(string imageFilePath, int size)
    {
        string cacheFolderPath = GetCacheFolderPathForImage(imageFilePath);
        string fileName = $"{Path.GetFileNameWithoutExtension(imageFilePath)}_{size}.webp";
        return Path.Combine(cacheFolderPath, fileName);
    }

    private static string GetCacheFolderPathForImage(string imageFilePath)
    {
        string? imageFolderPath = Path.GetDirectoryName(imageFilePath);

        if (string.IsNullOrWhiteSpace(imageFolderPath))
        {
            throw new InvalidOperationException("Image file path must have a parent folder.");
        }

        return GetCacheFolderPathForFolder(imageFolderPath);
    }

    private static string GetCacheFolderPathForFolder(string folderPath)
    {
        DirectoryInfo directoryInfo = new(folderPath);

        if (directoryInfo.Parent is null)
        {
            throw new InvalidOperationException("Folder path must have a parent folder.");
        }

        return Path.Combine(directoryInfo.Parent.FullName, CacheFolderName, directoryInfo.Name);
    }
}
