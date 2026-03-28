using CommunityToolkit.Mvvm.Messaging;
using DatasetStudio.Messages;
using DatasetStudio.Models;
using DatasetStudio.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DatasetStudio.Tests;

[TestFixture]
public class TagDictionaryServiceTests
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
    public async Task ResolveAlias_ReturnsCanonicalNameForKnownAlias()
    {
        using TemporaryDirectory temporaryDirectory = new();
        TestContext context = await CreateProjectContextAsync(temporaryDirectory.DirectoryPath);
        Project project = await context.ProjectService.CreateProjectAsync("ProjectOne", context.ProjectRootPath);
        project.TagDictionaryEntries.Add(new TagDictionaryEntry
        {
            CanonicalName = "feline",
            Aliases = new List<string> { "cat" },
        });
        await context.ProjectService.SaveProjectAsync(project);

        await context.TagDictionaryService.GetAllEntriesAsync(project.Id);

        string resolvedAlias = context.TagDictionaryService.ResolveAlias(project.Id, "cat");

        Assert.That(resolvedAlias, Is.EqualTo("feline"));
    }

    [Test]
    public async Task ResolveAlias_ReturnsInputForUnknownAlias()
    {
        using TemporaryDirectory temporaryDirectory = new();
        TestContext context = await CreateProjectContextAsync(temporaryDirectory.DirectoryPath);
        Project project = await context.ProjectService.CreateProjectAsync("ProjectOne", context.ProjectRootPath);

        await context.TagDictionaryService.GetAllEntriesAsync(project.Id);

        string resolvedAlias = context.TagDictionaryService.ResolveAlias(project.Id, "unknown-tag");

        Assert.That(resolvedAlias, Is.EqualTo("unknown-tag"));
    }

    [Test]
    public async Task RenameTagAsync_UpdatesTagInAllProjectFiles()
    {
        using TemporaryDirectory temporaryDirectory = new();
        TestContext context = await CreateProjectContextAsync(temporaryDirectory.DirectoryPath);
        Project project = await context.ProjectService.CreateProjectAsync("ProjectOne", context.ProjectRootPath);
        await CreateTaggedImageAsync(context.ProjectRootPath, "01_Inbox", "image001", new[] { "cat", "blue" });
        await CreateTaggedImageAsync(context.ProjectRootPath, "01_Inbox", "image002", new[] { "cat" });

        await context.TagDictionaryService.GetAllEntriesAsync(project.Id);
        await context.TagDictionaryService.RenameTagAsync(project.Id, "cat", "feline");

        IReadOnlyList<string> firstImageTags = await context.TagFileService.ReadTagsAsync(Path.Combine(context.ProjectRootPath, "01_Inbox", "image001.txt"));
        IReadOnlyList<string> secondImageTags = await context.TagFileService.ReadTagsAsync(Path.Combine(context.ProjectRootPath, "01_Inbox", "image002.txt"));

        Assert.That(firstImageTags, Is.EqualTo(new[] { "feline", "blue" }));
        Assert.That(secondImageTags, Is.EqualTo(new[] { "feline" }));
    }

    [Test]
    public async Task MergeTagsAsync_MergesSourceIntoTargetAcrossAllProjectFiles()
    {
        using TemporaryDirectory temporaryDirectory = new();
        TestContext context = await CreateProjectContextAsync(temporaryDirectory.DirectoryPath);
        Project project = await context.ProjectService.CreateProjectAsync("ProjectOne", context.ProjectRootPath);
        await CreateTaggedImageAsync(context.ProjectRootPath, "01_Inbox", "image001", new[] { "cat", "dog" });
        await CreateTaggedImageAsync(context.ProjectRootPath, "01_Inbox", "image002", new[] { "dog" });

        await context.TagDictionaryService.GetAllEntriesAsync(project.Id);
        await context.TagDictionaryService.MergeTagsAsync(project.Id, "dog", "cat");

        IReadOnlyList<string> firstImageTags = await context.TagFileService.ReadTagsAsync(Path.Combine(context.ProjectRootPath, "01_Inbox", "image001.txt"));
        IReadOnlyList<string> secondImageTags = await context.TagFileService.ReadTagsAsync(Path.Combine(context.ProjectRootPath, "01_Inbox", "image002.txt"));

        Assert.That(firstImageTags, Is.EqualTo(new[] { "cat" }));
        Assert.That(secondImageTags, Is.EqualTo(new[] { "cat" }));
    }

    [Test]
    public async Task MergeTagsAsync_WhenCircularAliasWouldBeCreated_ThrowsInvalidOperationException()
    {
        using TemporaryDirectory temporaryDirectory = new();
        TestContext context = await CreateProjectContextAsync(temporaryDirectory.DirectoryPath);
        Project project = await context.ProjectService.CreateProjectAsync("ProjectOne", context.ProjectRootPath);
        project.TagDictionaryEntries.Add(new TagDictionaryEntry
        {
            CanonicalName = "A",
            Aliases = new List<string> { "B" },
        });
        await context.ProjectService.SaveProjectAsync(project);

        await context.TagDictionaryService.GetAllEntriesAsync(project.Id);

        InvalidOperationException? exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await context.TagDictionaryService.MergeTagsAsync(project.Id, "A", "B"));

        Assert.That(exception?.Message, Does.Contain("circular alias"));
    }

    [Test]
    public async Task GetAllEntriesAsync_CountsCanonicalFrequencyAcrossProjectFiles()
    {
        using TemporaryDirectory temporaryDirectory = new();
        TestContext context = await CreateProjectContextAsync(temporaryDirectory.DirectoryPath);
        Project project = await context.ProjectService.CreateProjectAsync("ProjectOne", context.ProjectRootPath);
        await CreateTaggedImageAsync(context.ProjectRootPath, "01_Inbox", "image001", new[] { "cat" });
        await CreateTaggedImageAsync(context.ProjectRootPath, "01_Inbox", "image002", new[] { "cat", "dog" });
        await CreateTaggedImageAsync(context.ProjectRootPath, "01_Inbox", "image003", new[] { "dog" });
        await CreateTaggedImageAsync(context.ProjectRootPath, "01_Inbox", "image004", new[] { "cat" });
        await CreateTaggedImageAsync(context.ProjectRootPath, "01_Inbox", "image005", new[] { "cat" });

        IReadOnlyList<TagDictionaryEntry> entries = await context.TagDictionaryService.GetAllEntriesAsync(project.Id);
        TagDictionaryEntry catEntry = entries.Single(entry => entry.CanonicalName == "cat");

        Assert.That(catEntry.GlobalFrequency, Is.EqualTo(4));
    }

    [Test]
    public async Task GetAllEntriesAsync_PersistsTagStatisticsSnapshot()
    {
        using TemporaryDirectory temporaryDirectory = new();
        TestContext context = await CreateProjectContextAsync(temporaryDirectory.DirectoryPath);
        Project project = await context.ProjectService.CreateProjectAsync("ProjectOne", context.ProjectRootPath);
        await CreateTaggedImageAsync(context.ProjectRootPath, "01_Inbox", "image001", new[] { "cat" });
        await CreateTaggedImageAsync(context.ProjectRootPath, "01_Inbox", "image002", new[] { "cat", "dog" });

        IReadOnlyList<TagDictionaryEntry> entries = await context.TagDictionaryService.GetAllEntriesAsync(project.Id);
        IReadOnlyList<Project> persistedProjects = await context.ProjectService.LoadProjectsAsync();
        Project persistedProject = persistedProjects.Single(candidate => string.Equals(candidate.Id, project.Id, StringComparison.OrdinalIgnoreCase));
        TagDictionaryEntry persistedCatEntry = persistedProject.TagDictionaryEntries.Single(entry => entry.CanonicalName == "cat");

        Assert.That(entries.Count, Is.EqualTo(2));
        Assert.That(persistedProject.State?.TagStatisticsCacheFingerprint, Is.Not.Null.And.Not.Empty);
        Assert.That(persistedCatEntry.GlobalFrequency, Is.EqualTo(2));
    }

    [Test]
    public async Task GetAllEntriesAsync_RefreshesCacheAfterTagsChangedMessage()
    {
        using TemporaryDirectory temporaryDirectory = new();
        TestContext context = await CreateProjectContextAsync(temporaryDirectory.DirectoryPath);
        Project project = await context.ProjectService.CreateProjectAsync("ProjectOne", context.ProjectRootPath);
        await CreateTaggedImageAsync(context.ProjectRootPath, "01_Inbox", "image001", new[] { "cat" });
        await CreateTaggedImageAsync(context.ProjectRootPath, "01_Inbox", "image002", new[] { "cat" });

        await context.TagDictionaryService.GetAllEntriesAsync(project.Id);
        await context.TagFileService.WriteTagsAsync(Path.Combine(context.ProjectRootPath, "01_Inbox", "image002.txt"), new[] { "dog" });
        context.Messenger.Send(new TagsChangedMessage(Path.Combine(context.ProjectRootPath, "01_Inbox", "image002.png"), new[] { "dog" }));

        IReadOnlyList<TagDictionaryEntry> entries = await context.TagDictionaryService.GetAllEntriesAsync(project.Id);
        TagDictionaryEntry catEntry = entries.Single(entry => entry.CanonicalName == "cat");
        TagDictionaryEntry dogEntry = entries.Single(entry => entry.CanonicalName == "dog");

        Assert.That(catEntry.GlobalFrequency, Is.EqualTo(1));
        Assert.That(dogEntry.GlobalFrequency, Is.EqualTo(1));
    }

    [Test]
    public async Task GetAllEntriesAsync_RefreshesCacheAfterAiTaggingCompletedMessage()
    {
        using TemporaryDirectory temporaryDirectory = new();
        TestContext context = await CreateProjectContextAsync(temporaryDirectory.DirectoryPath);
        Project project = await context.ProjectService.CreateProjectAsync("ProjectOne", context.ProjectRootPath);
        await CreateTaggedImageAsync(context.ProjectRootPath, "01_Inbox", "image001", new[] { "cat" });
        await CreateTaggedImageAsync(context.ProjectRootPath, "01_Inbox", "image002", new[] { "cat" });

        await context.TagDictionaryService.GetAllEntriesAsync(project.Id);
        await context.TagFileService.WriteTagsAsync(Path.Combine(context.ProjectRootPath, "01_Inbox", "image002.txt"), new[] { "dog" });

        ImageTaggingResult taggingResult = new()
        {
            ModelId = "SmilingWolf/wd-swinv2-tagger-v3",
            AcceptedTrainingTags = new[] { "dog" },
        };

        context.Messenger.Send(new AiTaggingCompletedMessage(Path.Combine(context.ProjectRootPath, "01_Inbox", "image002.png"), taggingResult));

        IReadOnlyList<TagDictionaryEntry> entries = await context.TagDictionaryService.GetAllEntriesAsync(project.Id);
        TagDictionaryEntry catEntry = entries.Single(entry => entry.CanonicalName == "cat");
        TagDictionaryEntry dogEntry = entries.Single(entry => entry.CanonicalName == "dog");

        Assert.That(catEntry.GlobalFrequency, Is.EqualTo(1));
        Assert.That(dogEntry.GlobalFrequency, Is.EqualTo(1));
    }

    [Test]
    public async Task GetAllEntriesAsync_RefreshesCacheAfterImageDeletedMessage()
    {
        using TemporaryDirectory temporaryDirectory = new();
        TestContext context = await CreateProjectContextAsync(temporaryDirectory.DirectoryPath);
        Project project = await context.ProjectService.CreateProjectAsync("ProjectOne", context.ProjectRootPath);
        await CreateTaggedImageAsync(context.ProjectRootPath, "01_Inbox", "image001", new[] { "cat" });
        await CreateTaggedImageAsync(context.ProjectRootPath, "01_Inbox", "image002", new[] { "cat" });

        await context.TagDictionaryService.GetAllEntriesAsync(project.Id);
        string deletedImagePath = Path.Combine(context.ProjectRootPath, "01_Inbox", "image002.png");
        string deletedTagPath = Path.Combine(context.ProjectRootPath, "01_Inbox", "image002.txt");

        File.Delete(deletedImagePath);
        File.Delete(deletedTagPath);
        context.Messenger.Send(new ImageDeletedMessage(deletedImagePath, Path.Combine(context.ProjectRootPath, "01_Inbox")));

        IReadOnlyList<TagDictionaryEntry> entries = await context.TagDictionaryService.GetAllEntriesAsync(project.Id);
        TagDictionaryEntry catEntry = entries.Single(entry => entry.CanonicalName == "cat");

        Assert.That(catEntry.GlobalFrequency, Is.EqualTo(1));
    }

    [Test]
    public async Task GetAllEntriesAsync_RefreshesCacheAfterTagFilesChangedMessage()
    {
        using TemporaryDirectory temporaryDirectory = new();
        TestContext context = await CreateProjectContextAsync(temporaryDirectory.DirectoryPath);
        Project project = await context.ProjectService.CreateProjectAsync("ProjectOne", context.ProjectRootPath);
        await CreateTaggedImageAsync(context.ProjectRootPath, "01_Inbox", "image001", new[] { "cat" });
        await CreateTaggedImageAsync(context.ProjectRootPath, "01_Inbox", "image002", new[] { "cat" });

        await context.TagDictionaryService.GetAllEntriesAsync(project.Id);
        string changedTagPath = Path.Combine(context.ProjectRootPath, "01_Inbox", "image002.txt");
        await context.TagFileService.WriteTagsAsync(changedTagPath, new[] { "dog" });
        context.Messenger.Send(new TagFilesChangedMessage(changedTagPath));

        IReadOnlyList<TagDictionaryEntry> entries = await context.TagDictionaryService.GetAllEntriesAsync(project.Id);
        TagDictionaryEntry catEntry = entries.Single(entry => entry.CanonicalName == "cat");
        TagDictionaryEntry dogEntry = entries.Single(entry => entry.CanonicalName == "dog");

        Assert.That(catEntry.GlobalFrequency, Is.EqualTo(1));
        Assert.That(dogEntry.GlobalFrequency, Is.EqualTo(1));
    }

    [Test]
    public async Task DeleteTagAsync_WithRemoveFromFilesTrue_RemovesTagFromAllTagFiles()
    {
        using TemporaryDirectory temporaryDirectory = new();
        TestContext context = await CreateProjectContextAsync(temporaryDirectory.DirectoryPath);
        Project project = await context.ProjectService.CreateProjectAsync("ProjectOne", context.ProjectRootPath);
        await CreateTaggedImageAsync(context.ProjectRootPath, "01_Inbox", "image001", new[] { "cat", "dog" });
        await CreateTaggedImageAsync(context.ProjectRootPath, "01_Inbox", "image002", new[] { "cat" });

        await context.TagDictionaryService.GetAllEntriesAsync(project.Id);
        await context.TagDictionaryService.DeleteTagAsync(project.Id, "cat", true);

        IReadOnlyList<string> firstImageTags = await context.TagFileService.ReadTagsAsync(Path.Combine(context.ProjectRootPath, "01_Inbox", "image001.txt"));
        IReadOnlyList<string> secondImageTags = await context.TagFileService.ReadTagsAsync(Path.Combine(context.ProjectRootPath, "01_Inbox", "image002.txt"));

        Assert.That(firstImageTags, Is.EqualTo(new[] { "dog" }));
        Assert.That(secondImageTags, Is.Empty);
    }

    [Test]
    public async Task SetAliasesAsync_ReplacesPersistedAliasList()
    {
        using TemporaryDirectory temporaryDirectory = new();
        TestContext context = await CreateProjectContextAsync(temporaryDirectory.DirectoryPath);
        Project project = await context.ProjectService.CreateProjectAsync("ProjectOne", context.ProjectRootPath);
        project.TagDictionaryEntries.Add(new TagDictionaryEntry
        {
            CanonicalName = "feline",
            Aliases = new List<string> { "cat" },
        });
        await context.ProjectService.SaveProjectAsync(project);

        await context.TagDictionaryService.SetAliasesAsync(project.Id, "feline", new[] { "kitty", "tomcat" });

        IReadOnlyList<TagDictionaryEntry> entries = await context.TagDictionaryService.GetAllEntriesAsync(project.Id);
        TagDictionaryEntry entry = entries.Single(item => item.CanonicalName == "feline");

        Assert.That(entry.Aliases, Is.EqualTo(new[] { "kitty", "tomcat" }));
    }

    private static async Task<TestContext> CreateProjectContextAsync(string masterRootPath)
    {
        string projectRootPath = Path.Combine(masterRootPath, "ProjectOne");
        Directory.CreateDirectory(projectRootPath);
        Directory.CreateDirectory(Path.Combine(projectRootPath, "01_Inbox"));

        FileSystemService fileSystemService = new();
        StatePersistenceService statePersistenceService = new(fileSystemService, masterRootPath, TimeSpan.FromMilliseconds(20));
        await statePersistenceService.SaveAppStateAsync(new AppState
        {
            LastMasterRootDirectory = masterRootPath,
        });

        ProjectService projectService = new(fileSystemService, statePersistenceService);
        TagFileService tagFileService = new();
        WeakReferenceMessenger messenger = new();
        TagDictionaryService tagDictionaryService = new(projectService, tagFileService, messenger);

        return new TestContext(projectRootPath, projectService, tagFileService, tagDictionaryService, messenger);
    }

    private static async Task CreateTaggedImageAsync(string projectRootPath, string stageFolderName, string imageName, IReadOnlyList<string> tags)
    {
        string stageFolderPath = Path.Combine(projectRootPath, stageFolderName);
        Directory.CreateDirectory(stageFolderPath);
        string imageFilePath = Path.Combine(stageFolderPath, imageName + ".png");
        string tagFilePath = Path.Combine(stageFolderPath, imageName + ".txt");

        await File.WriteAllBytesAsync(imageFilePath, new byte[] { 1, 2, 3 });
        await File.WriteAllTextAsync(tagFilePath, string.Join(", ", tags));
    }

    private sealed record TestContext(
        string ProjectRootPath,
        ProjectService ProjectService,
        TagFileService TagFileService,
        TagDictionaryService TagDictionaryService,
        WeakReferenceMessenger Messenger);
}
