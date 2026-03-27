using CommunityToolkit.Mvvm.Messaging;
using DatasetStudio.Messages;
using DatasetStudio.Models;
using DatasetStudio.Services;
using DatasetStudio.Tests.TestDoubles;
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
        TestTagDictionaryService tagDictionaryService = new TestTagDictionaryService();
        BatchTagOperationService batchTagOperationService = new BatchTagOperationService(tagFileService, tagDictionaryService, messenger);
        TestStatePersistenceService statePersistenceService = new TestStatePersistenceService();
        LibraryGridViewModel viewModel = new LibraryGridViewModel(
            fileSystemService,
            tagFileService,
            tagDictionaryService,
            new TestThumbnailCacheService(),
            new TestClipboardService(),
            new TestNavigationService(),
            batchTagOperationService,
            messenger,
            statePersistenceService);

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

        statePersistenceService.SetProjectState(project.Id, project.State);

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
        TestTagDictionaryService tagDictionaryService = new TestTagDictionaryService();
        BatchTagOperationService batchTagOperationService = new BatchTagOperationService(tagFileService, tagDictionaryService, messenger);
        TestStatePersistenceService statePersistenceService = new TestStatePersistenceService();
        LibraryGridViewModel viewModel = new LibraryGridViewModel(
            fileSystemService,
            tagFileService,
            tagDictionaryService,
            new TestThumbnailCacheService(),
            new TestClipboardService(),
            new TestNavigationService(),
            batchTagOperationService,
            messenger,
            statePersistenceService);

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

        statePersistenceService.SetProjectState(project.Id, project.State);

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

    [Test]
    public async Task OnNavigatedTo_LoadsPersistedProjectStateFromStatePersistenceService()
    {
        StrongReferenceMessenger messenger = new StrongReferenceMessenger();
        TestFileSystemService fileSystemService = new TestFileSystemService();
        TestTagFileService tagFileService = new TestTagFileService();
        TestTagDictionaryService tagDictionaryService = new TestTagDictionaryService();
        TestStatePersistenceService statePersistenceService = new TestStatePersistenceService();
        BatchTagOperationService batchTagOperationService = new BatchTagOperationService(tagFileService, tagDictionaryService, messenger);
        LibraryGridViewModel viewModel = new LibraryGridViewModel(
            fileSystemService,
            tagFileService,
            tagDictionaryService,
            new TestThumbnailCacheService(),
            new TestClipboardService(),
            new TestNavigationService(),
            batchTagOperationService,
            messenger,
            statePersistenceService);

        string projectRootPath = Path.Combine("C:\\datasets", "animals");
        string inboxFolderPath = Path.Combine(projectRootPath, "01_Inbox");
        string reviewFolderPath = Path.Combine(projectRootPath, "02_Review");
        string dogImagePath = Path.Combine(reviewFolderPath, "dog.png");
        string catImagePath = Path.Combine(reviewFolderPath, "cat.png");

        fileSystemService.SetImageFiles(inboxFolderPath, Array.Empty<string>());
        fileSystemService.SetImageFiles(reviewFolderPath, new[] { catImagePath, dogImagePath });
        tagFileService.SetTags(catImagePath, new[] { "cat" });
        tagFileService.SetTags(dogImagePath, new[] { "dog" });

        Project project = new Project
        {
            Id = "project-state-1",
            Name = "Animals",
            RootFolderPath = projectRootPath,
            AiModelName = "fallback-model",
            Stages = new List<WorkflowStage>
            {
                new WorkflowStage { Order = 1, FolderName = "01_Inbox", DisplayName = "Inbox" },
                new WorkflowStage { Order = 2, FolderName = "02_Review", DisplayName = "Review" },
            },
            State = new ProjectState
            {
                ActiveStageFolderName = "01_Inbox",
                ZoomSliderValue = 120,
                SelectedAiModelName = "fallback-model",
                LastInspectedImagePath = null,
            },
        };

        statePersistenceService.SetProjectState(project.Id, new ProjectState
        {
            ActiveStageFolderName = "02_Review",
            ZoomSliderValue = 220,
            SelectedAiModelName = "persisted-model",
            LastInspectedImagePath = dogImagePath,
        });

        viewModel.OnNavigatedTo(project);

        await WaitForConditionAsync(() =>
            viewModel.ActiveStage?.FolderName == "02_Review"
            && viewModel.Images.Count == 2
            && viewModel.FocusedImageIndex >= 0).ConfigureAwait(false);

        Assert.That(viewModel.ZoomValue, Is.EqualTo(220));
        Assert.That(viewModel.SelectedAiModel?.Id, Is.EqualTo("persisted-model"));
        Assert.That(viewModel.Images[viewModel.FocusedImageIndex].FilePath, Is.EqualTo(dogImagePath));
    }

    [Test]
    public async Task OpenInspectorCommand_NavigatesWithProjectAndPreservesFocusedImagePath()
    {
        StrongReferenceMessenger messenger = new StrongReferenceMessenger();
        TestFileSystemService fileSystemService = new TestFileSystemService();
        TestTagFileService tagFileService = new TestTagFileService();
        TestNavigationService navigationService = new TestNavigationService();
        TestTagDictionaryService tagDictionaryService = new TestTagDictionaryService();
        BatchTagOperationService batchTagOperationService = new BatchTagOperationService(tagFileService, tagDictionaryService, messenger);
        TestStatePersistenceService statePersistenceService = new TestStatePersistenceService();
        LibraryGridViewModel viewModel = new LibraryGridViewModel(
            fileSystemService,
            tagFileService,
            tagDictionaryService,
            new TestThumbnailCacheService(),
            new TestClipboardService(),
            navigationService,
            batchTagOperationService,
            messenger,
            statePersistenceService);

        string projectRootPath = Path.Combine("C:\\datasets", "animals");
        string reviewFolderPath = Path.Combine(projectRootPath, "02_Review");
        string reviewImagePath = Path.Combine(reviewFolderPath, "cat.png");
        fileSystemService.SetImageFiles(reviewFolderPath, new[] { reviewImagePath });
        tagFileService.SetTags(reviewImagePath, new[] { "cat" });

        Project project = new Project
        {
            Id = "project-3",
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

        statePersistenceService.SetProjectState(project.Id, project.State);

        viewModel.OnNavigatedTo(project);
        await WaitForConditionAsync(() => viewModel.Images.Count == 1).ConfigureAwait(false);

        viewModel.OpenInspectorCommand.Execute(viewModel.Images[0]);

        Assert.That(project.State.LastInspectedImagePath, Is.EqualTo(reviewImagePath));
        Assert.That(navigationService.LastNavigationTargetType, Is.EqualTo(typeof(InspectorModeViewModel)));
        Assert.That(navigationService.LastNavigationParameter, Is.SameAs(project));
    }

    [Test]
    public async Task NavigateGridCommand_ChangesFocusedImageByOffset()
    {
        StrongReferenceMessenger messenger = new StrongReferenceMessenger();
        TestFileSystemService fileSystemService = new TestFileSystemService();
        TestTagFileService tagFileService = new TestTagFileService();
        TestTagDictionaryService tagDictionaryService = new TestTagDictionaryService();
        BatchTagOperationService batchTagOperationService = new BatchTagOperationService(tagFileService, tagDictionaryService, messenger);
        TestStatePersistenceService statePersistenceService = new TestStatePersistenceService();
        LibraryGridViewModel viewModel = new LibraryGridViewModel(
            fileSystemService,
            tagFileService,
            tagDictionaryService,
            new TestThumbnailCacheService(),
            new TestClipboardService(),
            new TestNavigationService(),
            batchTagOperationService,
            messenger,
            statePersistenceService);

        string projectRootPath = Path.Combine("C:\\datasets", "animals");
        string reviewFolderPath = Path.Combine(projectRootPath, "02_Review");
        string catImagePath = Path.Combine(reviewFolderPath, "cat.png");
        string dogImagePath = Path.Combine(reviewFolderPath, "dog.png");
        string birdImagePath = Path.Combine(reviewFolderPath, "bird.png");
        fileSystemService.SetImageFiles(reviewFolderPath, new[] { catImagePath, dogImagePath, birdImagePath });
        tagFileService.SetTags(catImagePath, new[] { "cat" });
        tagFileService.SetTags(dogImagePath, new[] { "dog" });
        tagFileService.SetTags(birdImagePath, new[] { "bird" });

        Project project = new Project
        {
            Id = "project-3b",
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

        statePersistenceService.SetProjectState(project.Id, project.State);

        viewModel.OnNavigatedTo(project);
        await WaitForConditionAsync(() => viewModel.Images.Count == 3).ConfigureAwait(false);

        viewModel.NavigateGridCommand.Execute(1);
        Assert.That(viewModel.Images[1].IsFocused, Is.True);
        Assert.That(viewModel.FocusedImageIndex, Is.EqualTo(1));

        viewModel.NavigateGridCommand.Execute(5);
        Assert.That(viewModel.Images[2].IsFocused, Is.True);
        Assert.That(viewModel.FocusedImageIndex, Is.EqualTo(2));
    }

    [Test]
    public async Task CommitBatchAddCommand_AddsTagAcrossActiveFolderWhenSelectionIsEmpty()
    {
        StrongReferenceMessenger messenger = new StrongReferenceMessenger();
        TestFileSystemService fileSystemService = new TestFileSystemService();
        TestTagFileService tagFileService = new TestTagFileService();
        TestTagDictionaryService tagDictionaryService = new TestTagDictionaryService();
        BatchTagOperationService batchTagOperationService = new BatchTagOperationService(tagFileService, tagDictionaryService, messenger);
        TestStatePersistenceService statePersistenceService = new TestStatePersistenceService();
        LibraryGridViewModel viewModel = new LibraryGridViewModel(
            fileSystemService,
            tagFileService,
            tagDictionaryService,
            new TestThumbnailCacheService(),
            new TestClipboardService(),
            new TestNavigationService(),
            batchTagOperationService,
            messenger,
            statePersistenceService);

        string projectRootPath = Path.Combine("C:\\datasets", "animals");
        string reviewFolderPath = Path.Combine(projectRootPath, "02_Review");
        string catImagePath = Path.Combine(reviewFolderPath, "cat.png");
        string dogImagePath = Path.Combine(reviewFolderPath, "dog.png");
        fileSystemService.SetImageFiles(reviewFolderPath, new[] { catImagePath, dogImagePath });
        tagFileService.SetTags(catImagePath, new[] { "cat" });
        tagFileService.SetTags(dogImagePath, Array.Empty<string>());

        Project project = new Project
        {
            Id = "project-4",
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

        statePersistenceService.SetProjectState(project.Id, project.State);

        viewModel.OnNavigatedTo(project);
        await WaitForConditionAsync(() => viewModel.Images.Count == 2).ConfigureAwait(false);

        viewModel.OpenBatchAddCommand.Execute(null);
        await WaitForConditionAsync(() => viewModel.IsBatchAddOpen).ConfigureAwait(false);
        viewModel.BatchAddQueryText = "sunset";
        await viewModel.CommitBatchAddCommand.ExecuteAsync(null).ConfigureAwait(false);

        Assert.That(await tagFileService.ReadTagsAsync(tagFileService.GetTagFilePath(catImagePath)).ConfigureAwait(false), Is.EqualTo(new[] { "cat", "sunset" }));
        Assert.That(await tagFileService.ReadTagsAsync(tagFileService.GetTagFilePath(dogImagePath)).ConfigureAwait(false), Is.EqualTo(new[] { "sunset" }));
    }

    [Test]
    public async Task CommitBatchRemoveCommand_UsesSelectionScopeWhenImagesAreSelected()
    {
        StrongReferenceMessenger messenger = new StrongReferenceMessenger();
        TestFileSystemService fileSystemService = new TestFileSystemService();
        TestTagFileService tagFileService = new TestTagFileService();
        TestTagDictionaryService tagDictionaryService = new TestTagDictionaryService();
        BatchTagOperationService batchTagOperationService = new BatchTagOperationService(tagFileService, tagDictionaryService, messenger);
        TestStatePersistenceService statePersistenceService = new TestStatePersistenceService();
        LibraryGridViewModel viewModel = new LibraryGridViewModel(
            fileSystemService,
            tagFileService,
            tagDictionaryService,
            new TestThumbnailCacheService(),
            new TestClipboardService(),
            new TestNavigationService(),
            batchTagOperationService,
            messenger,
            statePersistenceService);

        string projectRootPath = Path.Combine("C:\\datasets", "animals");
        string reviewFolderPath = Path.Combine(projectRootPath, "02_Review");
        string catImagePath = Path.Combine(reviewFolderPath, "cat.png");
        string dogImagePath = Path.Combine(reviewFolderPath, "dog.png");
        fileSystemService.SetImageFiles(reviewFolderPath, new[] { catImagePath, dogImagePath });
        tagFileService.SetTags(catImagePath, new[] { "cat", "orange" });
        tagFileService.SetTags(dogImagePath, new[] { "cat", "brown" });

        Project project = new Project
        {
            Id = "project-5",
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

        statePersistenceService.SetProjectState(project.Id, project.State);

        viewModel.OnNavigatedTo(project);
        await WaitForConditionAsync(() => viewModel.Images.Count == 2).ConfigureAwait(false);

        viewModel.ToggleSelectionCommand.Execute(viewModel.Images[0]);
        viewModel.OpenBatchRemoveCommand.Execute(null);
        await WaitForConditionAsync(() => viewModel.IsBatchRemoveOpen).ConfigureAwait(false);
        viewModel.BatchRemoveQueryText = "cat";
        await viewModel.CommitBatchRemoveCommand.ExecuteAsync(null).ConfigureAwait(false);

        Assert.That(await tagFileService.ReadTagsAsync(tagFileService.GetTagFilePath(catImagePath)).ConfigureAwait(false), Is.EqualTo(new[] { "orange" }));
        Assert.That(await tagFileService.ReadTagsAsync(tagFileService.GetTagFilePath(dogImagePath)).ConfigureAwait(false), Is.EqualTo(new[] { "cat", "brown" }));
    }

    [Test]
    public async Task MoveImageCommand_MovesSelectedImageAndTagFileToAdjacentStage()
    {
        StrongReferenceMessenger messenger = new StrongReferenceMessenger();
        TestFileSystemService fileSystemService = new TestFileSystemService();
        TestTagFileService tagFileService = new TestTagFileService();
        TestTagDictionaryService tagDictionaryService = new TestTagDictionaryService();
        BatchTagOperationService batchTagOperationService = new BatchTagOperationService(tagFileService, tagDictionaryService, messenger);
        TestStatePersistenceService statePersistenceService = new TestStatePersistenceService();
        LibraryGridViewModel viewModel = new LibraryGridViewModel(
            fileSystemService,
            tagFileService,
            tagDictionaryService,
            new TestThumbnailCacheService(),
            new TestClipboardService(),
            new TestNavigationService(),
            batchTagOperationService,
            messenger,
            statePersistenceService);

        string projectRootPath = Path.Combine("C:\\datasets", "animals");
        string reviewFolderPath = Path.Combine(projectRootPath, "02_Review");
        string trainFolderPath = Path.Combine(projectRootPath, "03_Train");
        string catImagePath = Path.Combine(reviewFolderPath, "cat.png");
        fileSystemService.SetImageFiles(reviewFolderPath, new[] { catImagePath });
        fileSystemService.SetImageFiles(trainFolderPath, Array.Empty<string>());
        tagFileService.SetTags(catImagePath, new[] { "cat" });

        Project project = new Project
        {
            Id = "project-6",
            Name = "Animals",
            RootFolderPath = projectRootPath,
            Stages = new List<WorkflowStage>
            {
                new WorkflowStage { Order = 2, FolderName = "02_Review", DisplayName = "Review" },
                new WorkflowStage { Order = 3, FolderName = "03_Train", DisplayName = "Train" },
            },
            State = new ProjectState
            {
                ActiveStageFolderName = "02_Review",
            },
        };

        statePersistenceService.SetProjectState(project.Id, project.State);

        viewModel.OnNavigatedTo(project);
        await WaitForConditionAsync(() => viewModel.Images.Count == 1).ConfigureAwait(false);

        viewModel.ToggleSelectionCommand.Execute(viewModel.Images[0]);
        await viewModel.MoveImageCommand.ExecuteAsync(1).ConfigureAwait(false);

        Assert.That(fileSystemService.MovedFiles, Does.Contain((catImagePath, trainFolderPath)));
        Assert.That(fileSystemService.MovedFiles, Does.Contain((tagFileService.GetTagFilePath(catImagePath), trainFolderPath)));
    }

    [Test]
    public async Task CopyAndPasteTags_UsesFocusedImageAndSkipsPrefixTagsOnWrite()
    {
        StrongReferenceMessenger messenger = new StrongReferenceMessenger();
        TestFileSystemService fileSystemService = new TestFileSystemService();
        TestTagFileService tagFileService = new TestTagFileService();
        TestTagDictionaryService tagDictionaryService = new TestTagDictionaryService();
        TestClipboardService clipboardService = new TestClipboardService();
        BatchTagOperationService batchTagOperationService = new BatchTagOperationService(tagFileService, tagDictionaryService, messenger);
        TestStatePersistenceService statePersistenceService = new TestStatePersistenceService();
        LibraryGridViewModel viewModel = new LibraryGridViewModel(
            fileSystemService,
            tagFileService,
            tagDictionaryService,
            new TestThumbnailCacheService(),
            clipboardService,
            new TestNavigationService(),
            batchTagOperationService,
            messenger,
            statePersistenceService);

        string projectRootPath = Path.Combine("C:\\datasets", "animals");
        string reviewFolderPath = Path.Combine(projectRootPath, "02_Review");
        string catImagePath = Path.Combine(reviewFolderPath, "cat.png");
        fileSystemService.SetImageFiles(reviewFolderPath, new[] { catImagePath });
        tagFileService.SetTags(catImagePath, new[] { "cat" });
        tagDictionaryService.SetAlias("kitty", "cat");
        clipboardService.TagsToPaste = new[] { "dataset", "kitty", "backlit" };

        Project project = new Project
        {
            Id = "project-7",
            Name = "Animals",
            RootFolderPath = projectRootPath,
            PrefixTags = new List<string> { "dataset" },
            Stages = new List<WorkflowStage>
            {
                new WorkflowStage { Order = 2, FolderName = "02_Review", DisplayName = "Review" },
            },
            State = new ProjectState
            {
                ActiveStageFolderName = "02_Review",
            },
        };

        statePersistenceService.SetProjectState(project.Id, project.State);

        viewModel.OnNavigatedTo(project);
        await WaitForConditionAsync(() => viewModel.Images.Count == 1).ConfigureAwait(false);

        await viewModel.CopyTagsCommand.ExecuteAsync(null).ConfigureAwait(false);
        Assert.That(clipboardService.LastCopiedTags, Is.EqualTo(new[] { "dataset", "cat" }));

        await viewModel.PasteTagsCommand.ExecuteAsync(null).ConfigureAwait(false);
        Assert.That(await tagFileService.ReadTagsAsync(tagFileService.GetTagFilePath(catImagePath)).ConfigureAwait(false), Is.EqualTo(new[] { "cat", "backlit" }));
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

        public List<(string SourcePath, string DestinationFolder)> MovedFiles { get; } = new List<(string SourcePath, string DestinationFolder)>();

        public List<string> RecycledFiles { get; } = new List<string>();

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
            MovedFiles.Add((sourcePath, destinationFolder));

            string sourceFolderPath = Path.GetDirectoryName(sourcePath) ?? string.Empty;
            string destinationPath = Path.Combine(destinationFolder, Path.GetFileName(sourcePath));

            if (Path.GetExtension(sourcePath).Equals(".txt", StringComparison.OrdinalIgnoreCase))
            {
                return Task.CompletedTask;
            }

            IReadOnlyList<string> sourceImages = imageFilesByFolderPath.TryGetValue(sourceFolderPath, out IReadOnlyList<string>? existingSourceImages)
                ? existingSourceImages
                : Array.Empty<string>();
            IReadOnlyList<string> destinationImages = imageFilesByFolderPath.TryGetValue(destinationFolder, out IReadOnlyList<string>? existingDestinationImages)
                ? existingDestinationImages
                : Array.Empty<string>();

            imageFilesByFolderPath[sourceFolderPath] = sourceImages
                .Where(imagePath => !string.Equals(imagePath, sourcePath, StringComparison.OrdinalIgnoreCase))
                .ToList();
            imageFilesByFolderPath[destinationFolder] = destinationImages
                .Concat(new[] { destinationPath })
                .ToList();
            return Task.CompletedTask;
        }

        public Task RecycleFileAsync(string filePath)
        {
            RecycledFiles.Add(filePath);

            if (!Path.GetExtension(filePath).Equals(".txt", StringComparison.OrdinalIgnoreCase))
            {
                string folderPath = Path.GetDirectoryName(filePath) ?? string.Empty;
                IReadOnlyList<string> images = imageFilesByFolderPath.TryGetValue(folderPath, out IReadOnlyList<string>? existingImages)
                    ? existingImages
                    : Array.Empty<string>();
                imageFilesByFolderPath[folderPath] = images
                    .Where(imagePath => !string.Equals(imagePath, filePath, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

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
        private readonly Dictionary<string, string> canonicalNameByAlias = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> availableTags = new List<string>();

        public void SetAlias(string alias, string canonicalTag)
        {
            canonicalNameByAlias[alias] = canonicalTag;
        }

        public void SetAvailableTags(IEnumerable<string> tags)
        {
            availableTags.Clear();
            availableTags.AddRange(tags);
        }

        public Task<IReadOnlyList<TagDictionaryEntry>> GetAllEntriesAsync(string projectId)
        {
            _ = projectId;
            return Task.FromResult<IReadOnlyList<TagDictionaryEntry>>(Array.Empty<TagDictionaryEntry>());
        }

        public Task<IReadOnlyList<string>> SearchTagsAsync(string projectId, string query)
        {
            _ = projectId;
            IEnumerable<string> matchingTags = availableTags;

            if (!string.IsNullOrWhiteSpace(query))
            {
                matchingTags = matchingTags.Where(tag => tag.Contains(query, StringComparison.OrdinalIgnoreCase));
            }

            return Task.FromResult<IReadOnlyList<string>>(matchingTags.ToList());
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

            if (canonicalNameByAlias.TryGetValue(input, out string? canonicalName))
            {
                return canonicalName;
            }

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
        public IReadOnlyList<string> LastCopiedTags { get; private set; } = Array.Empty<string>();

        public IReadOnlyList<string> TagsToPaste { get; set; } = Array.Empty<string>();

        public Task CopyTagsAsync(IReadOnlyList<string> tags)
        {
            LastCopiedTags = tags.ToList();
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<string>> PasteTagsAsync()
        {
            return Task.FromResult(TagsToPaste);
        }
    }

    private sealed class TestNavigationService : INavigationService
    {
        public Type? LastNavigationTargetType { get; private set; }

        public object? LastNavigationParameter { get; private set; }

        public void NavigateTo<TViewModel>() where TViewModel : ScreenViewModelBase
        {
            LastNavigationTargetType = typeof(TViewModel);
        }

        public void NavigateTo<TViewModel>(object parameter) where TViewModel : ScreenViewModelBase
        {
            LastNavigationTargetType = typeof(TViewModel);
            LastNavigationParameter = parameter;
        }

        public void GoBack()
        {
        }
    }
}
