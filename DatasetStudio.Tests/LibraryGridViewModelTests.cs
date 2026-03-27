using CommunityToolkit.Mvvm.Messaging;
using DatasetStudio.Messages;
using DatasetStudio.Models;
using DatasetStudio.Services;
using DatasetStudio.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DatasetStudio.Tests;

[TestFixture]
public class LibraryGridViewModelTests
{
    [Test]
    public async Task OnNavigatedTo_LoadsConfiguredStageImagesAndAppliesFilter()
    {
        StrongReferenceMessenger messenger = new StrongReferenceMessenger();
        TestFileSystemService fileSystemService = new TestFileSystemService();
        TestTagFileService tagFileService = new TestTagFileService();
        LibraryGridViewModel viewModel = new LibraryGridViewModel(
            fileSystemService,
            tagFileService,
            new TestTagDictionaryService(),
            new TestThumbnailCacheService(),
            new TestClipboardService(),
            new TestNavigationService(),
            messenger);

        string projectRootPath = Path.Combine("C:\\datasets", "animals");
        string inboxFolderPath = Path.Combine(projectRootPath, "01_Inbox");
        string reviewFolderPath = Path.Combine(projectRootPath, "02_Review");
        string reviewImageOnePath = Path.Combine(reviewFolderPath, "cat.png");
        string reviewImageTwoPath = Path.Combine(reviewFolderPath, "dog.png");
        string inboxImagePath = Path.Combine(inboxFolderPath, "bird.png");

        fileSystemService.SetImageFiles(inboxFolderPath, new[] { inboxImagePath });
        fileSystemService.SetImageFiles(reviewFolderPath, new[] { reviewImageOnePath, reviewImageTwoPath });
        tagFileService.SetTags(reviewImageOnePath, new[] { "cat", "orange" });
        tagFileService.SetTags(reviewImageTwoPath, new[] { "dog" });
        tagFileService.SetTags(inboxImagePath, Array.Empty<string>());

        Project project = new Project
        {
            Id = "project-1",
            Name = "Animals",
            RootFolderPath = projectRootPath,
            PrefixTags = new List<string> { "dataset" },
            Stages = new List<WorkflowStage>
            {
                new WorkflowStage { Order = 1, FolderName = "01_Inbox", DisplayName = "Inbox" },
                new WorkflowStage { Order = 2, FolderName = "02_Review", DisplayName = "Review" },
            },
            State = new ProjectState
            {
                ActiveStageFolderName = "02_Review",
                ZoomSliderValue = 180,
            },
        };

        viewModel.OnNavigatedTo(project);

        await WaitForConditionAsync(() => viewModel.ActiveStage is not null && viewModel.Images.Count == 2).ConfigureAwait(false);

        Assert.That(viewModel.ProjectName, Is.EqualTo("Animals"));
        Assert.That(viewModel.ZoomValue, Is.EqualTo(180));
        Assert.That(viewModel.ActiveStage?.FolderName, Is.EqualTo("02_Review"));
        Assert.That(viewModel.Stages.Select(stage => stage.ImageCount), Is.EqualTo(new[] { 1, 2 }));
        Assert.That(viewModel.Images.Select(image => image.FileName), Is.EqualTo(new[] { "cat.png", "dog.png" }));
        Assert.That(viewModel.Images[0].Tags, Is.EqualTo(new[] { "dataset", "cat", "orange" }));

        viewModel.FilterText = "orange";

        Assert.That(viewModel.Images.Select(image => image.FileName), Is.EqualTo(new[] { "cat.png" }));
    }

    [Test]
    public async Task ToggleSelectionCommand_UpdatesSelectionAndPublishesMessage()
    {
        StrongReferenceMessenger messenger = new StrongReferenceMessenger();
        TestFileSystemService fileSystemService = new TestFileSystemService();
        TestTagFileService tagFileService = new TestTagFileService();
        LibraryGridViewModel viewModel = new LibraryGridViewModel(
            fileSystemService,
            tagFileService,
            new TestTagDictionaryService(),
            new TestThumbnailCacheService(),
            new TestClipboardService(),
            new TestNavigationService(),
            messenger);

        string projectRootPath = Path.Combine("C:\\datasets", "animals");
        string reviewFolderPath = Path.Combine(projectRootPath, "02_Review");
        string reviewImagePath = Path.Combine(reviewFolderPath, "cat.png");

        fileSystemService.SetImageFiles(reviewFolderPath, new[] { reviewImagePath });
        tagFileService.SetTags(reviewImagePath, new[] { "cat" });

        Project project = new Project
        {
            Id = "project-2",
            Name = "Animals",
            RootFolderPath = projectRootPath,
            Stages = new List<WorkflowStage>
            {
                new WorkflowStage { Order = 2, FolderName = "02_Review", DisplayName = "Review" },
            },
            State = new ProjectState
            {
                ActiveStageFolderName = "02_Review",
            },
        };

        SelectionMessageRecorder selectionMessageRecorder = new SelectionMessageRecorder();
        messenger.Register<SelectionMessageRecorder, ImageSelectionChangedMessage>(selectionMessageRecorder, static (recipient, message) =>
        {
            recipient.LastMessage = message;
        });

        viewModel.OnNavigatedTo(project);

        await WaitForConditionAsync(() => viewModel.Images.Count == 1).ConfigureAwait(false);

        viewModel.ToggleSelectionCommand.Execute(viewModel.Images[0]);

        Assert.That(viewModel.SelectedImages.Count, Is.EqualTo(1));
        Assert.That(viewModel.Images[0].IsSelected, Is.True);
        Assert.That(selectionMessageRecorder.LastMessage?.ImagePath, Is.EqualTo(reviewImagePath));
        Assert.That(selectionMessageRecorder.LastMessage?.IsSelected, Is.True);
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

    private sealed class SelectionMessageRecorder
    {
        public ImageSelectionChangedMessage? LastMessage { get; set; }
    }

    private sealed class TestFileSystemService : IFileSystemService
    {
        private readonly Dictionary<string, IReadOnlyList<string>> imageFilesByFolderPath = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

        public void SetImageFiles(string folderPath, IReadOnlyList<string> imageFiles)
        {
            imageFilesByFolderPath[folderPath] = imageFiles;
        }

        public Task<IReadOnlyList<string>> DiscoverProjectFoldersAsync(string masterRootPath)
        {
            _ = masterRootPath;
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        public Task<IReadOnlyList<string>> GetImageFilesAsync(string folderPath)
        {
            if (imageFilesByFolderPath.TryGetValue(folderPath, out IReadOnlyList<string>? imageFiles))
            {
                return Task.FromResult(imageFiles);
            }

            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        public Task MoveFileAsync(string sourcePath, string destinationFolder)
        {
            _ = sourcePath;
            _ = destinationFolder;
            return Task.CompletedTask;
        }

        public Task RecycleFileAsync(string filePath)
        {
            _ = filePath;
            return Task.CompletedTask;
        }

        public Task EnsureFolderExistsAsync(string folderPath)
        {
            _ = folderPath;
            return Task.CompletedTask;
        }

        public FileSystemWatcher WatchFolder(string folderPath)
        {
            _ = folderPath;
            return new FileSystemWatcher();
        }
    }

    private sealed class TestTagFileService : ITagFileService
    {
        private readonly Dictionary<string, IReadOnlyList<string>> tagsByTagFilePath = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

        public void SetTags(string imageFilePath, IReadOnlyList<string> tags)
        {
            tagsByTagFilePath[GetTagFilePath(imageFilePath)] = tags;
        }

        public Task<IReadOnlyList<string>> ReadTagsAsync(string tagFilePath)
        {
            if (tagsByTagFilePath.TryGetValue(tagFilePath, out IReadOnlyList<string>? tags))
            {
                return Task.FromResult(tags);
            }

            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        public Task WriteTagsAsync(string tagFilePath, IReadOnlyList<string> tags)
        {
            tagsByTagFilePath[tagFilePath] = tags;
            return Task.CompletedTask;
        }

        public async Task<IReadOnlyList<string>> ReadTagsWithPrefixAsync(string tagFilePath, IReadOnlyList<string> prefixTags)
        {
            IReadOnlyList<string> tags = await ReadTagsAsync(tagFilePath).ConfigureAwait(false);
            return prefixTags.Concat(tags).ToList();
        }

        public string GetTagFilePath(string imageFilePath)
        {
            return Path.ChangeExtension(imageFilePath, ".txt");
        }

        public bool TagFileExists(string imageFilePath)
        {
            return tagsByTagFilePath.ContainsKey(GetTagFilePath(imageFilePath));
        }
    }

    private sealed class TestTagDictionaryService : ITagDictionaryService
    {
        public Task<IReadOnlyList<TagDictionaryEntry>> GetAllEntriesAsync(string projectId)
        {
            _ = projectId;
            return Task.FromResult<IReadOnlyList<TagDictionaryEntry>>(Array.Empty<TagDictionaryEntry>());
        }

        public Task<IReadOnlyList<string>> SearchTagsAsync(string projectId, string query)
        {
            _ = projectId;
            _ = query;
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        public Task RenameTagAsync(string projectId, string oldName, string newName)
        {
            _ = projectId;
            _ = oldName;
            _ = newName;
            return Task.CompletedTask;
        }

        public Task MergeTagsAsync(string projectId, string sourceTag, string targetTag)
        {
            _ = projectId;
            _ = sourceTag;
            _ = targetTag;
            return Task.CompletedTask;
        }

        public Task DeleteTagAsync(string projectId, string tagName, bool removeFromFiles)
        {
            _ = projectId;
            _ = tagName;
            _ = removeFromFiles;
            return Task.CompletedTask;
        }

        public Task AddAliasAsync(string projectId, string canonicalTag, string alias)
        {
            _ = projectId;
            _ = canonicalTag;
            _ = alias;
            return Task.CompletedTask;
        }

        public Task SetAliasesAsync(string projectId, string canonicalTag, IReadOnlyList<string> aliases)
        {
            _ = projectId;
            _ = canonicalTag;
            _ = aliases;
            return Task.CompletedTask;
        }

        public string ResolveAlias(string projectId, string input)
        {
            _ = projectId;
            return input;
        }
    }

    private sealed class TestThumbnailCacheService : IThumbnailCacheService
    {
        public Task<Stream> GetThumbnailAsync(string imageFilePath, int size)
        {
            _ = imageFilePath;
            _ = size;
            return Task.FromResult<Stream>(new MemoryStream(Array.Empty<byte>()));
        }

        public Task InvalidateAsync(string imageFilePath)
        {
            _ = imageFilePath;
            return Task.CompletedTask;
        }

        public Task InvalidateFolderAsync(string folderPath)
        {
            _ = folderPath;
            return Task.CompletedTask;
        }
    }

    private sealed class TestClipboardService : IClipboardService
    {
        public Task CopyTagsAsync(IReadOnlyList<string> tags)
        {
            _ = tags;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<string>> PasteTagsAsync()
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }
    }

    private sealed class TestNavigationService : INavigationService
    {
        public void NavigateTo<TViewModel>() where TViewModel : ViewModelBase
        {
        }

        public void NavigateTo<TViewModel>(object parameter) where TViewModel : ViewModelBase
        {
            _ = parameter;
        }

        public void GoBack()
        {
        }
    }
}
