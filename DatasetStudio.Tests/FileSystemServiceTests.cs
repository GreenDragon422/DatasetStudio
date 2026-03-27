using DatasetStudio.Services;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DatasetStudio.Tests;

[TestFixture]
public class FileSystemServiceTests
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
    public async Task GetImageFilesAsync_ReturnsOnlySupportedImageExtensions()
    {
        using TemporaryDirectory temporaryDirectory = new();
        FileSystemService fileSystemService = new();

        await File.WriteAllTextAsync(Path.Combine(temporaryDirectory.DirectoryPath, "image1.png"), "a");
        await File.WriteAllTextAsync(Path.Combine(temporaryDirectory.DirectoryPath, "image2.JPG"), "b");
        await File.WriteAllTextAsync(Path.Combine(temporaryDirectory.DirectoryPath, "image3.webp"), "c");
        await File.WriteAllTextAsync(Path.Combine(temporaryDirectory.DirectoryPath, "notes.txt"), "d");
        await File.WriteAllTextAsync(Path.Combine(temporaryDirectory.DirectoryPath, "data.json"), "e");

        IReadOnlyList<string> imageFiles = await fileSystemService.GetImageFilesAsync(temporaryDirectory.DirectoryPath);

        Assert.That(imageFiles.Select(Path.GetFileName), Is.EqualTo(new[] { "image1.png", "image2.JPG", "image3.webp" }));
    }

    [Test]
    public async Task MoveFileAsync_MovesFileIntoDestinationFolder()
    {
        using TemporaryDirectory temporaryDirectory = new();
        FileSystemService fileSystemService = new();
        string sourceFolderPath = Path.Combine(temporaryDirectory.DirectoryPath, "source");
        string destinationFolderPath = Path.Combine(temporaryDirectory.DirectoryPath, "destination");
        string sourceFilePath = Path.Combine(sourceFolderPath, "image.png");
        string destinationFilePath = Path.Combine(destinationFolderPath, "image.png");

        Directory.CreateDirectory(sourceFolderPath);
        await File.WriteAllTextAsync(sourceFilePath, "image");

        await fileSystemService.MoveFileAsync(sourceFilePath, destinationFolderPath);

        Assert.That(File.Exists(sourceFilePath), Is.False);
        Assert.That(File.Exists(destinationFilePath), Is.True);
    }

    [Test]
    public async Task EnsureFolderExistsAsync_CreatesFolderAndDoesNotThrowWhenItAlreadyExists()
    {
        using TemporaryDirectory temporaryDirectory = new();
        FileSystemService fileSystemService = new();
        string folderPath = Path.Combine(temporaryDirectory.DirectoryPath, "nested", "folder");

        await fileSystemService.EnsureFolderExistsAsync(folderPath);
        await fileSystemService.EnsureFolderExistsAsync(folderPath);

        Assert.That(Directory.Exists(folderPath), Is.True);
    }

    [Test]
    public async Task DiscoverProjectFoldersAsync_ReturnsOnlyFoldersContainingDatasetStudioConfiguration()
    {
        using TemporaryDirectory temporaryDirectory = new();
        FileSystemService fileSystemService = new();
        string projectOnePath = Path.Combine(temporaryDirectory.DirectoryPath, "ProjectOne");
        string projectTwoPath = Path.Combine(temporaryDirectory.DirectoryPath, "ProjectTwo");
        string notAProjectPath = Path.Combine(temporaryDirectory.DirectoryPath, "Scratch");

        Directory.CreateDirectory(projectOnePath);
        Directory.CreateDirectory(projectTwoPath);
        Directory.CreateDirectory(notAProjectPath);

        await File.WriteAllTextAsync(Path.Combine(projectOnePath, ".datasetstudio.json"), "{}");
        await File.WriteAllTextAsync(Path.Combine(projectTwoPath, ".datasetstudio.json"), "{}");
        await File.WriteAllTextAsync(Path.Combine(notAProjectPath, "notes.txt"), "ignore");

        IReadOnlyList<string> discoveredProjects = await fileSystemService.DiscoverProjectFoldersAsync(temporaryDirectory.DirectoryPath);

        Assert.That(discoveredProjects, Is.EqualTo(new[] { projectOnePath, projectTwoPath }.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray()));
    }
}
