using DatasetStudio.Services;
using SixLabors.ImageSharp;
using System;
using System.IO;
using System.Threading.Tasks;

namespace DatasetStudio.Tests;

[TestFixture]
public class ThumbnailCacheServiceTests
{
    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            DirectoryPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(DirectoryPath);
        }

        public string DirectoryPath { get; }

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, true);
            }
        }
    }

    [Test]
    public async Task GetThumbnailAsync_WhenCacheMiss_GeneratesThumbnailFileInCacheFolder()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string imageFilePath = CreateBitmapImage(temporaryDirectory.DirectoryPath, "01_Inbox", "image001.bmp", 4, 2);
        ThumbnailCacheService thumbnailCacheService = new();

        await using Stream thumbnailStream = await thumbnailCacheService.GetThumbnailAsync(imageFilePath, 160);
        string thumbnailPath = Path.Combine(temporaryDirectory.DirectoryPath, ".datasetstudio-cache", "01_Inbox", "image001_160.webp");

        Assert.That(File.Exists(thumbnailPath), Is.True);
        Assert.That(thumbnailStream.Length, Is.GreaterThan(0));

        using Image image = Image.Load(thumbnailPath);

        Assert.That(image.Width, Is.EqualTo(160));
        Assert.That(image.Height, Is.EqualTo(160));
    }

    [Test]
    public async Task GetThumbnailAsync_WhenCacheHit_ReusesExistingThumbnailFile()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string imageFilePath = CreateBitmapImage(temporaryDirectory.DirectoryPath, "01_Inbox", "image001.bmp", 4, 2);
        ThumbnailCacheService thumbnailCacheService = new();
        string thumbnailPath = Path.Combine(temporaryDirectory.DirectoryPath, ".datasetstudio-cache", "01_Inbox", "image001_160.webp");

        await using (Stream initialThumbnailStream = await thumbnailCacheService.GetThumbnailAsync(imageFilePath, 160))
        {
        }

        DateTime firstWriteTimeUtc = File.GetLastWriteTimeUtc(thumbnailPath);
        await Task.Delay(50);

        await using Stream secondThumbnailStream = await thumbnailCacheService.GetThumbnailAsync(imageFilePath, 160);
        DateTime secondWriteTimeUtc = File.GetLastWriteTimeUtc(thumbnailPath);

        Assert.That(secondThumbnailStream.Length, Is.GreaterThan(0));
        Assert.That(secondWriteTimeUtc, Is.EqualTo(firstWriteTimeUtc));
    }

    [Test]
    public async Task GetThumbnailAsync_WhenSourceImageChanges_RegeneratesThumbnail()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string imageFilePath = CreateBitmapImage(temporaryDirectory.DirectoryPath, "01_Inbox", "image001.bmp", 4, 2);
        ThumbnailCacheService thumbnailCacheService = new();
        string thumbnailPath = Path.Combine(temporaryDirectory.DirectoryPath, ".datasetstudio-cache", "01_Inbox", "image001_160.webp");

        await using (Stream initialThumbnailStream = await thumbnailCacheService.GetThumbnailAsync(imageFilePath, 160))
        {
        }

        DateTime firstWriteTimeUtc = File.GetLastWriteTimeUtc(thumbnailPath);
        await Task.Delay(50);
        await File.WriteAllBytesAsync(imageFilePath, CreateBitmapBytes(6, 3));
        File.SetLastWriteTimeUtc(imageFilePath, DateTime.UtcNow.AddSeconds(1));

        await using Stream regeneratedThumbnailStream = await thumbnailCacheService.GetThumbnailAsync(imageFilePath, 160);
        DateTime secondWriteTimeUtc = File.GetLastWriteTimeUtc(thumbnailPath);

        Assert.That(regeneratedThumbnailStream.Length, Is.GreaterThan(0));
        Assert.That(secondWriteTimeUtc, Is.GreaterThan(firstWriteTimeUtc));
    }

    [Test]
    public async Task InvalidateAsync_RemovesCachedThumbnailFilesForImage()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string imageFilePath = CreateBitmapImage(temporaryDirectory.DirectoryPath, "01_Inbox", "image001.bmp", 4, 2);
        ThumbnailCacheService thumbnailCacheService = new();
        string thumbnailPath = Path.Combine(temporaryDirectory.DirectoryPath, ".datasetstudio-cache", "01_Inbox", "image001_160.webp");

        await using (Stream thumbnailStream = await thumbnailCacheService.GetThumbnailAsync(imageFilePath, 160))
        {
        }

        await thumbnailCacheService.InvalidateAsync(imageFilePath);

        Assert.That(File.Exists(thumbnailPath), Is.False);
    }

    private static string CreateBitmapImage(string projectRootPath, string stageFolderName, string fileName, int width, int height)
    {
        string stageFolderPath = Path.Combine(projectRootPath, stageFolderName);
        Directory.CreateDirectory(stageFolderPath);
        string imageFilePath = Path.Combine(stageFolderPath, fileName);
        File.WriteAllBytes(imageFilePath, CreateBitmapBytes(width, height));
        return imageFilePath;
    }

    private static byte[] CreateBitmapBytes(int width, int height)
    {
        const int bitsPerPixel = 24;
        const int bitmapFileHeaderSize = 14;
        const int bitmapInfoHeaderSize = 40;
        int bytesPerPixel = bitsPerPixel / 8;
        int rowSize = ((width * bytesPerPixel + 3) / 4) * 4;
        int pixelArraySize = rowSize * height;
        int fileSize = bitmapFileHeaderSize + bitmapInfoHeaderSize + pixelArraySize;

        using MemoryStream memoryStream = new();
        using BinaryWriter writer = new(memoryStream);

        writer.Write((byte)'B');
        writer.Write((byte)'M');
        writer.Write(fileSize);
        writer.Write((short)0);
        writer.Write((short)0);
        writer.Write(bitmapFileHeaderSize + bitmapInfoHeaderSize);

        writer.Write(bitmapInfoHeaderSize);
        writer.Write(width);
        writer.Write(height);
        writer.Write((short)1);
        writer.Write((short)bitsPerPixel);
        writer.Write(0);
        writer.Write(pixelArraySize);
        writer.Write(2835);
        writer.Write(2835);
        writer.Write(0);
        writer.Write(0);

        int rowPadding = rowSize - (width * bytesPerPixel);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                byte blue = (byte)(20 + (x * 20));
                byte green = (byte)(40 + (y * 30));
                byte red = (byte)(60 + (x * 10) + (y * 10));

                writer.Write(blue);
                writer.Write(green);
                writer.Write(red);
            }

            for (int paddingIndex = 0; paddingIndex < rowPadding; paddingIndex++)
            {
                writer.Write((byte)0);
            }
        }

        return memoryStream.ToArray();
    }
}
