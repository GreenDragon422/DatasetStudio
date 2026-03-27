using CommunityToolkit.Mvvm.Messaging;
using DatasetStudio.Models;
using DatasetStudio.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace DatasetStudio.Tests;

[TestFixture]
public class BatchTagOperationServiceTests
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

    private sealed class FakeTagDictionaryService : ITagDictionaryService
    {
        private readonly Dictionary<string, string> canonicalNameByAlias;

        public FakeTagDictionaryService(Dictionary<string, string>? canonicalNameByAlias = null)
        {
            this.canonicalNameByAlias = canonicalNameByAlias ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public Task AddAliasAsync(string projectId, string canonicalTag, string alias)
        {
            canonicalNameByAlias[alias] = canonicalTag;
            return Task.CompletedTask;
        }

        public Task SetAliasesAsync(string projectId, string canonicalTag, IReadOnlyList<string> aliases)
        {
            foreach (KeyValuePair<string, string> aliasEntry in canonicalNameByAlias.Where(pair => string.Equals(pair.Value, canonicalTag, StringComparison.OrdinalIgnoreCase)).ToList())
            {
                canonicalNameByAlias.Remove(aliasEntry.Key);
            }

            foreach (string alias in aliases)
            {
                canonicalNameByAlias[alias] = canonicalTag;
            }

            return Task.CompletedTask;
        }

        public Task DeleteTagAsync(string projectId, string tagName, bool removeFromFiles)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<TagDictionaryEntry>> GetAllEntriesAsync(string projectId)
        {
            return Task.FromResult<IReadOnlyList<TagDictionaryEntry>>([]);
        }

        public Task MergeTagsAsync(string projectId, string sourceTag, string targetTag)
        {
            return Task.CompletedTask;
        }

        public Task RenameTagAsync(string projectId, string oldName, string newName)
        {
            return Task.CompletedTask;
        }

        public string ResolveAlias(string projectId, string input)
        {
            if (canonicalNameByAlias.TryGetValue(input, out string? canonicalName))
            {
                return canonicalName;
            }

            return input;
        }

        public Task<IReadOnlyList<string>> SearchTagsAsync(string projectId, string query)
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }
    }

    [Test]
    public async Task AddTagAsync_SkipsDuplicateTags()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string imageFilePath = CreateImageFilePath(temporaryDirectory.DirectoryPath, "image001");
        TagFileService tagFileService = new();
        await tagFileService.WriteTagsAsync(tagFileService.GetTagFilePath(imageFilePath), new[] { "cat" });
        BatchTagOperationService batchTagOperationService = new(tagFileService, new FakeTagDictionaryService(), new WeakReferenceMessenger());

        await batchTagOperationService.AddTagAsync("project-1", new[] { imageFilePath }, "cat");

        IReadOnlyList<string> tags = await tagFileService.ReadTagsAsync(tagFileService.GetTagFilePath(imageFilePath));
        Assert.That(tags, Is.EqualTo(new[] { "cat" }));
    }

    [Test]
    public async Task AddTagAsync_ResolvesAliasBeforeAdding()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string imageFilePath = CreateImageFilePath(temporaryDirectory.DirectoryPath, "image001");
        TagFileService tagFileService = new();
        BatchTagOperationService batchTagOperationService = new(
            tagFileService,
            new FakeTagDictionaryService(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["kitty"] = "cat",
            }),
            new WeakReferenceMessenger());

        await batchTagOperationService.AddTagAsync("project-1", new[] { imageFilePath }, "kitty");

        IReadOnlyList<string> tags = await tagFileService.ReadTagsAsync(tagFileService.GetTagFilePath(imageFilePath));
        Assert.That(tags, Is.EqualTo(new[] { "cat" }));
    }

    [Test]
    public async Task RemoveTagAsync_RemovesOnlyTheTargetTag()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string imageFilePath = CreateImageFilePath(temporaryDirectory.DirectoryPath, "image001");
        TagFileService tagFileService = new();
        await tagFileService.WriteTagsAsync(tagFileService.GetTagFilePath(imageFilePath), new[] { "cat", "dog", "bird" });
        BatchTagOperationService batchTagOperationService = new(tagFileService, new FakeTagDictionaryService(), new WeakReferenceMessenger());

        await batchTagOperationService.RemoveTagAsync("project-1", new[] { imageFilePath }, "dog");

        IReadOnlyList<string> tags = await tagFileService.ReadTagsAsync(tagFileService.GetTagFilePath(imageFilePath));
        Assert.That(tags, Is.EqualTo(new[] { "cat", "bird" }));
    }

    [Test]
    public async Task RemoveTagAsync_WhenTagDoesNotExist_IsNoOp()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string imageFilePath = CreateImageFilePath(temporaryDirectory.DirectoryPath, "image001");
        TagFileService tagFileService = new();
        await tagFileService.WriteTagsAsync(tagFileService.GetTagFilePath(imageFilePath), new[] { "cat", "dog" });
        BatchTagOperationService batchTagOperationService = new(tagFileService, new FakeTagDictionaryService(), new WeakReferenceMessenger());

        await batchTagOperationService.RemoveTagAsync("project-1", new[] { imageFilePath }, "bird");

        IReadOnlyList<string> tags = await tagFileService.ReadTagsAsync(tagFileService.GetTagFilePath(imageFilePath));
        Assert.That(tags, Is.EqualTo(new[] { "cat", "dog" }));
    }

    private static string CreateImageFilePath(string directoryPath, string fileNameWithoutExtension)
    {
        string imageFilePath = Path.Combine(directoryPath, fileNameWithoutExtension + ".png");
        File.WriteAllBytes(imageFilePath, new byte[] { 1, 2, 3 });
        return imageFilePath;
    }
}
