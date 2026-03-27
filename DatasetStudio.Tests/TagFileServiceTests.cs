using DatasetStudio.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace DatasetStudio.Tests;

[TestFixture]
public class TagFileServiceTests
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

    private static string CreateImagePath(string directoryPath, string fileName)
    {
        return Path.Combine(directoryPath, fileName);
    }

    [TestCase("image.png", "image.txt")]
    [TestCase("image.jpg", "image.txt")]
    [TestCase("image.jpeg", "image.txt")]
    [TestCase("image.webp", "image.txt")]
    [TestCase("image.bmp", "image.txt")]
    public void GetTagFilePath_ReplacesImageExtensionWithTxt(string imageFileName, string expectedTagFileName)
    {
        using TemporaryDirectory temporaryDirectory = new();
        TagFileService tagFileService = new();
        string imagePath = CreateImagePath(temporaryDirectory.DirectoryPath, imageFileName);

        string actualTagFilePath = tagFileService.GetTagFilePath(imagePath);

        Assert.That(Path.GetFileName(actualTagFilePath), Is.EqualTo(expectedTagFileName));
    }

    [Test]
    public async Task ReadAndWriteRoundTrip_ReturnsEquivalentTags()
    {
        using TemporaryDirectory temporaryDirectory = new();
        TagFileService tagFileService = new();
        string tagFilePath = Path.Combine(temporaryDirectory.DirectoryPath, "image.txt");
        IReadOnlyList<string> expectedTags = ["tag1", "tag2", "another tag"];

        await tagFileService.WriteTagsAsync(tagFilePath, expectedTags);
        IReadOnlyList<string> actualTags = await tagFileService.ReadTagsAsync(tagFilePath);

        Assert.That(actualTags, Is.EqualTo(expectedTags));
    }

    [Test]
    public async Task ReadTagsAsync_TrimsWhitespaceAroundCommaSeparatedTags()
    {
        using TemporaryDirectory temporaryDirectory = new();
        TagFileService tagFileService = new();
        string tagFilePath = Path.Combine(temporaryDirectory.DirectoryPath, "image.txt");

        await File.WriteAllTextAsync(tagFilePath, "tag1 , tag2 ,  tag3 ");
        IReadOnlyList<string> actualTags = await tagFileService.ReadTagsAsync(tagFilePath);

        Assert.That(actualTags, Is.EqualTo(new[] { "tag1", "tag2", "tag3" }));
    }

    [Test]
    public async Task ReadTagsWithPrefixAsync_PrependsPrefixTags()
    {
        using TemporaryDirectory temporaryDirectory = new();
        TagFileService tagFileService = new();
        string tagFilePath = Path.Combine(temporaryDirectory.DirectoryPath, "image.txt");

        await tagFileService.WriteTagsAsync(tagFilePath, ["c", "d"]);
        IReadOnlyList<string> actualTags = await tagFileService.ReadTagsWithPrefixAsync(tagFilePath, ["a", "b"]);

        Assert.That(actualTags, Is.EqualTo(new[] { "a", "b", "c", "d" }));
    }

    [Test]
    public async Task ReadTagsAsync_ReturnsEmptyListForEmptyFile()
    {
        using TemporaryDirectory temporaryDirectory = new();
        TagFileService tagFileService = new();
        string tagFilePath = Path.Combine(temporaryDirectory.DirectoryPath, "image.txt");

        await File.WriteAllTextAsync(tagFilePath, string.Empty);
        IReadOnlyList<string> actualTags = await tagFileService.ReadTagsAsync(tagFilePath);

        Assert.That(actualTags, Is.Empty);
    }

    [Test]
    public async Task ReadTagsAsync_ReturnsEmptyListWhenFileIsMissing()
    {
        using TemporaryDirectory temporaryDirectory = new();
        TagFileService tagFileService = new();
        string tagFilePath = Path.Combine(temporaryDirectory.DirectoryPath, "missing.txt");

        IReadOnlyList<string> actualTags = await tagFileService.ReadTagsAsync(tagFilePath);

        Assert.That(actualTags, Is.Empty);
    }

    [Test]
    public async Task ReadTagsAsync_ExcludesWhitespaceOnlyTags()
    {
        using TemporaryDirectory temporaryDirectory = new();
        TagFileService tagFileService = new();
        string tagFilePath = Path.Combine(temporaryDirectory.DirectoryPath, "image.txt");

        await File.WriteAllTextAsync(tagFilePath, "tag1,   , tag2,");
        IReadOnlyList<string> actualTags = await tagFileService.ReadTagsAsync(tagFilePath);

        Assert.That(actualTags, Is.EqualTo(new[] { "tag1", "tag2" }));
    }

    [Test]
    public async Task TagFileExists_ReturnsTrueWhenCompanionFileExists()
    {
        using TemporaryDirectory temporaryDirectory = new();
        TagFileService tagFileService = new();
        string imagePath = CreateImagePath(temporaryDirectory.DirectoryPath, "image.png");
        string tagFilePath = tagFileService.GetTagFilePath(imagePath);

        await File.WriteAllTextAsync(tagFilePath, "tag1");
        bool tagFileExists = tagFileService.TagFileExists(imagePath);

        Assert.That(tagFileExists, Is.True);
    }
}
