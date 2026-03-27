using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using DatasetStudio.Models;
using DatasetStudio.Services;
using DatasetStudio.Tests.TestDoubles;
using DatasetStudio.ViewModels;
using NUnit.Framework;

namespace DatasetStudio.Tests;

[TestFixture]
public class InspectorModeViewModelTests
{
    [Test]
    public async Task OnNavigatedTo_LoadsLastInspectedImageAndPrefixTags()
    {
        InspectorTestProjectContext testProjectContext = await CreateProjectContextAsync().ConfigureAwait(false);

        InspectorModeViewModel viewModel = CreateViewModel(testProjectContext);
        viewModel.OnNavigatedTo(testProjectContext.Project);

        await WaitForConditionAsync(() => viewModel.CurrentImage is not null).ConfigureAwait(false);

        Assert.That(viewModel.CurrentImage?.FilePath, Is.EqualTo(testProjectContext.CatImagePath));
        Assert.That(viewModel.PrefixTagsText, Is.EqualTo("dataset"));
        Assert.That(viewModel.ActiveStage?.FolderName, Is.EqualTo("02_Review"));
    }

    [Test]
    public async Task CommitTagCommand_ResolvesAliasPersistsTagAndAdvancesToNextPendingImage()
    {
        InspectorTestProjectContext testProjectContext = await CreateProjectContextAsync().ConfigureAwait(false);

        InspectorModeViewModel viewModel = CreateViewModel(testProjectContext);
        viewModel.OnNavigatedTo(testProjectContext.Project);
        await WaitForConditionAsync(() => viewModel.CurrentImage is not null).ConfigureAwait(false);

        viewModel.TagInputText = "cat";
        await viewModel.CommitTagCommand.ExecuteAsync(null).ConfigureAwait(false);
        await WaitForConditionAsync(() => viewModel.CurrentImage is not null && string.Equals(viewModel.CurrentImage.FilePath, testProjectContext.DogImagePath, StringComparison.OrdinalIgnoreCase)).ConfigureAwait(false);

        IReadOnlyList<string> persistedTags = await testProjectContext.TagFileService.ReadTagsAsync(testProjectContext.TagFileService.GetTagFilePath(testProjectContext.CatImagePath)).ConfigureAwait(false);
        Assert.That(persistedTags, Is.EqualTo(new[] { "feline" }));
        Assert.That(viewModel.CurrentImage?.FilePath, Is.EqualTo(testProjectContext.DogImagePath));
    }

    [Test]
    public async Task PasteTagsCommand_FiltersPrefixTagsAndResolvesAliases()
    {
        InspectorTestProjectContext testProjectContext = await CreateProjectContextAsync().ConfigureAwait(false);
        testProjectContext.ClipboardService.PastedTags = new[] { "dataset", "cat", "backlit" };

        InspectorModeViewModel viewModel = CreateViewModel(testProjectContext);
        viewModel.OnNavigatedTo(testProjectContext.Project);
        await WaitForConditionAsync(() => viewModel.CurrentImage is not null).ConfigureAwait(false);

        await viewModel.PasteTagsCommand.ExecuteAsync(null).ConfigureAwait(false);

        IReadOnlyList<string> persistedTags = await testProjectContext.TagFileService.ReadTagsAsync(testProjectContext.TagFileService.GetTagFilePath(testProjectContext.CatImagePath)).ConfigureAwait(false);
        Assert.That(persistedTags, Is.EqualTo(new[] { "feline", "backlit" }));
        Assert.That(viewModel.AppliedTags, Is.EqualTo(new[] { "feline", "backlit" }));
    }

    private static InspectorModeViewModel CreateViewModel(InspectorTestProjectContext testProjectContext)
    {
        return new InspectorModeViewModel(
            testProjectContext.TagFileService,
            testProjectContext.TagDictionaryService,
            testProjectContext.FileSystemService,
            testProjectContext.ClipboardService,
            new StubNavigationService(),
            testProjectContext.Messenger);
    }

    private static async Task<InspectorTestProjectContext> CreateProjectContextAsync()
    {
        string workspacePath = Path.Combine(Path.GetTempPath(), "DatasetStudioInspectorTests", Guid.NewGuid().ToString("N"));
        string rootFolderPath = Path.Combine(workspacePath, "animal-study");
        string reviewFolderPath = Path.Combine(rootFolderPath, "02_Review");

        Directory.CreateDirectory(reviewFolderPath);

        string catImagePath = Path.Combine(reviewFolderPath, "cat.png");
        string dogImagePath = Path.Combine(reviewFolderPath, "dog.png");

        await File.WriteAllBytesAsync(catImagePath, TinyPngBytes).ConfigureAwait(false);
        await File.WriteAllBytesAsync(dogImagePath, TinyPngBytes).ConfigureAwait(false);

        Project project = new Project
        {
            Id = "project-inspector",
            Name = "Animal Study",
            RootFolderPath = rootFolderPath,
            PrefixTags = new List<string> { "dataset" },
            Stages = new List<WorkflowStage>
            {
                new WorkflowStage { Order = 1, FolderName = "02_Review", DisplayName = "Review" },
            },
            TagDictionaryEntries = new List<TagDictionaryEntry>
            {
                new TagDictionaryEntry
                {
                    CanonicalName = "feline",
                    Aliases = new List<string> { "cat" },
                },
                new TagDictionaryEntry
                {
                    CanonicalName = "backlit",
                    Aliases = new List<string>(),
                },
            },
            State = new ProjectState
            {
                ActiveStageFolderName = "02_Review",
                LastInspectedImagePath = catImagePath,
            },
        };

        FileSystemService fileSystemService = new FileSystemService();
        TagFileService tagFileService = new TagFileService();
        StrongReferenceMessenger messenger = new StrongReferenceMessenger();
        InMemoryProjectService projectService = new InMemoryProjectService(new[] { project });
        TagDictionaryService tagDictionaryService = new TagDictionaryService(projectService, tagFileService, messenger);
        RecordingClipboardService clipboardService = new RecordingClipboardService();

        return new InspectorTestProjectContext(
            project,
            fileSystemService,
            tagFileService,
            tagDictionaryService,
            clipboardService,
            messenger,
            catImagePath,
            dogImagePath);
    }

    private static async Task WaitForConditionAsync(Func<bool> condition)
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(10).ConfigureAwait(false);
        }

        Assert.Fail("Condition was not met within the allotted time.");
    }

    private static readonly byte[] TinyPngBytes =
    {
        137, 80, 78, 71, 13, 10, 26, 10,
        0, 0, 0, 13, 73, 72, 68, 82, 0, 0, 0, 1, 0, 0, 0, 1, 8, 6, 0, 0, 0, 31, 21, 196, 137,
        0, 0, 0, 13, 73, 68, 65, 84, 120, 156, 99, 248, 207, 192, 240, 31, 0, 5, 0, 1, 255, 137, 153, 61, 29,
        0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130,
    };

}
