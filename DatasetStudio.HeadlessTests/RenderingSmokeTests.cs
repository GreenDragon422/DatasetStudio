using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Messaging;
using DatasetStudio.Messages;
using DatasetStudio.Models;
using DatasetStudio.Services;
using DatasetStudio.ViewModels;
using DatasetStudio.Views;
using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace DatasetStudio_HeadlessTests;

public class RenderingSmokeTests
{
    [AvaloniaTest]
    public async Task ShouldRenderExistingScreens()
    {
        TestOutputHelper.ClearTestOutputs();

        using CaptureScenario scenario = await CaptureScenario.CreateAsync().ConfigureAwait(true);
        string outputFolder = TestOutputHelper.CreateRunOutputFolder("existing-screens", clearExisting: true);

        await CaptureProjectsHubAsync(scenario, outputFolder).ConfigureAwait(true);
        await CaptureProjectConfigurationAsync(scenario, outputFolder).ConfigureAwait(true);
        await CaptureTagDictionaryAsync(scenario, outputFolder).ConfigureAwait(true);
        await CaptureLibraryGridAsync(scenario, outputFolder).ConfigureAwait(true);
        await CaptureInspectorModeAsync(scenario, outputFolder).ConfigureAwait(true);

        Assert.That(File.Exists(Path.Combine(outputFolder, "projects-hub.png")), Is.True);
        Assert.That(File.Exists(Path.Combine(outputFolder, "project-configuration.png")), Is.True);
        Assert.That(File.Exists(Path.Combine(outputFolder, "tag-dictionary.png")), Is.True);
        Assert.That(File.Exists(Path.Combine(outputFolder, "library-grid.png")), Is.True);
        Assert.That(File.Exists(Path.Combine(outputFolder, "inspector-mode.png")), Is.True);
    }

    [AvaloniaTest]
    public async Task LibraryGridSlashShortcutFocusesFilterTextBox()
    {
        using CaptureScenario scenario = await CaptureScenario.CreateAsync().ConfigureAwait(true);
        LibraryGridViewModel libraryGridViewModel = new LibraryGridViewModel(
            scenario.FileSystemService,
            scenario.TagFileService,
            scenario.TagDictionaryService,
            scenario.ThumbnailCacheService,
            scenario.ClipboardService,
            scenario.NavigationService,
            new BatchTagOperationService(scenario.TagFileService, scenario.TagDictionaryService, scenario.Messenger),
            scenario.Messenger);
        libraryGridViewModel.OnNavigatedTo(scenario.PrimaryProject);

        MainWindow window = scenario.CreateMainWindow(libraryGridViewModel);
        window.Show();
        await Task.Delay(500).ConfigureAwait(true);

        TextBox? filterTextBox = FindDescendantByName<TextBox>(window, "FilterTextBox");
        Assert.That(filterTextBox, Is.Not.Null);

        bool didFocusWindow = window.Focus();
        _ = didFocusWindow;
        window.KeyPress(Key.Oem2, RawInputModifiers.None, PhysicalKey.Slash, "/");
        await Task.Delay(50).ConfigureAwait(true);

        Assert.That(filterTextBox!.IsKeyboardFocusWithin, Is.True);

        window.Close();
    }

    [AvaloniaTest]
    public async Task LibraryGridGlobalCopyShortcutCopiesFocusedImageTags()
    {
        using CaptureScenario scenario = await CaptureScenario.CreateAsync().ConfigureAwait(true);
        LibraryGridViewModel libraryGridViewModel = new LibraryGridViewModel(
            scenario.FileSystemService,
            scenario.TagFileService,
            scenario.TagDictionaryService,
            scenario.ThumbnailCacheService,
            scenario.ClipboardService,
            scenario.NavigationService,
            new BatchTagOperationService(scenario.TagFileService, scenario.TagDictionaryService, scenario.Messenger),
            scenario.Messenger);
        libraryGridViewModel.OnNavigatedTo(scenario.PrimaryProject);

        MainWindow window = scenario.CreateMainWindow(libraryGridViewModel);
        window.Show();
        await Task.Delay(500).ConfigureAwait(true);

        IReadOnlyList<string> expectedTags = libraryGridViewModel.Images[libraryGridViewModel.FocusedImageIndex].Tags;
        bool didFocusWindow = window.Focus();
        _ = didFocusWindow;
        window.KeyPress(Key.C, RawInputModifiers.Control | RawInputModifiers.Shift, PhysicalKey.C, "c");
        await Task.Delay(50).ConfigureAwait(true);

        Assert.That(scenario.ClipboardService.LastCopiedTags, Is.EqualTo(expectedTags));

        window.Close();
    }

    [AvaloniaTest]
    public async Task InspectorEscapeLeavesTagInputBeforeNavigatingBack()
    {
        using CaptureScenario scenario = await CaptureScenario.CreateAsync().ConfigureAwait(true);
        string preferredImagePath = Path.Combine(scenario.PrimaryProject.RootFolderPath, "02_Review", "cat.png");
        scenario.PrimaryProject.State.ActiveStageFolderName = "02_Review";
        scenario.PrimaryProject.State.LastInspectedImagePath = preferredImagePath;

        InspectorModeViewModel inspectorModeViewModel = new InspectorModeViewModel(
            scenario.TagFileService,
            scenario.TagDictionaryService,
            scenario.FileSystemService,
            scenario.ClipboardService,
            scenario.NavigationService,
            scenario.Messenger);
        inspectorModeViewModel.OnNavigatedTo(scenario.PrimaryProject);

        MainWindow window = scenario.CreateMainWindow(inspectorModeViewModel);
        window.Show();
        await Task.Delay(500).ConfigureAwait(true);

        TextBox? tagInputTextBox = FindDescendantByName<TextBox>(window, "TagInputTextBox");
        Assert.That(tagInputTextBox, Is.Not.Null);
        Assert.That(tagInputTextBox.IsKeyboardFocusWithin, Is.True);

        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        await Task.Delay(50).ConfigureAwait(true);

        Assert.That(tagInputTextBox!.IsKeyboardFocusWithin, Is.False);
        Assert.That(scenario.NavigationService.GoBackCount, Is.EqualTo(0));

        bool didFocusWindow = window.Focus();
        _ = didFocusWindow;
        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        await Task.Delay(50).ConfigureAwait(true);

        Assert.That(scenario.NavigationService.GoBackCount, Is.EqualTo(1));

        window.Close();
    }

    private static TControl? FindDescendantByName<TControl>(Window window, string controlName)
        where TControl : Control
    {
        foreach (TControl control in window.GetVisualDescendants().OfType<TControl>())
        {
            if (!string.Equals(control.Name, controlName, StringComparison.Ordinal))
            {
                continue;
            }
            return control;
        }

        return null;
    }

    private static async Task CaptureProjectsHubAsync(CaptureScenario scenario, string outputFolder)
    {
        ProjectsHubViewModel projectsHubViewModel = new ProjectsHubViewModel(
            scenario.ProjectService,
            scenario.FileSystemService,
            scenario.NavigationService,
            scenario.Messenger);

        MainWindow window = scenario.CreateMainWindow(projectsHubViewModel);
        await CaptureWindowAsync(window, outputFolder, "projects-hub.png", 350).ConfigureAwait(true);
    }

    private static async Task CaptureProjectConfigurationAsync(CaptureScenario scenario, string outputFolder)
    {
        ProjectsHubViewModel projectsHubViewModel = new ProjectsHubViewModel(
            scenario.ProjectService,
            scenario.FileSystemService,
            scenario.NavigationService,
            scenario.Messenger);

        ProjectConfigurationViewModel projectConfigurationViewModel = new ProjectConfigurationViewModel(
            scenario.ProjectService,
            scenario.AiTaggerService,
            scenario.FileSystemService,
            scenario.Messenger);
        projectConfigurationViewModel.LoadProject(scenario.PrimaryProject);

        MainWindowViewModel mainWindowViewModel = scenario.CreateMainWindowViewModel();
        mainWindowViewModel.CurrentView = projectsHubViewModel;
        mainWindowViewModel.OpenProjectConfiguration(projectConfigurationViewModel);

        MainWindow window = new MainWindow(mainWindowViewModel)
        {
            Width = 1440,
            Height = 900,
        };

        await CaptureWindowAsync(window, outputFolder, "project-configuration.png", 450).ConfigureAwait(true);
    }

    private static async Task CaptureTagDictionaryAsync(CaptureScenario scenario, string outputFolder)
    {
        TagDictionaryViewModel tagDictionaryViewModel = new TagDictionaryViewModel(
            scenario.TagDictionaryService,
            scenario.Messenger);
        tagDictionaryViewModel.OnNavigatedTo(scenario.PrimaryProject.Id);

        MainWindow window = scenario.CreateMainWindow(tagDictionaryViewModel);
        await CaptureWindowAsync(window, outputFolder, "tag-dictionary.png", 350).ConfigureAwait(true);
    }

    private static async Task CaptureLibraryGridAsync(CaptureScenario scenario, string outputFolder)
    {
        LibraryGridViewModel libraryGridViewModel = new LibraryGridViewModel(
            scenario.FileSystemService,
            scenario.TagFileService,
            scenario.TagDictionaryService,
            scenario.ThumbnailCacheService,
            scenario.ClipboardService,
            scenario.NavigationService,
            new BatchTagOperationService(scenario.TagFileService, scenario.TagDictionaryService, scenario.Messenger),
            scenario.Messenger);
        libraryGridViewModel.OnNavigatedTo(scenario.PrimaryProject);

        MainWindow window = scenario.CreateMainWindow(libraryGridViewModel);
        await CaptureWindowAsync(window, outputFolder, "library-grid.png", 500).ConfigureAwait(true);
    }

    private static async Task CaptureInspectorModeAsync(CaptureScenario scenario, string outputFolder)
    {
        string preferredImagePath = Path.Combine(scenario.PrimaryProject.RootFolderPath, "02_Review", "cat.png");
        scenario.PrimaryProject.State.ActiveStageFolderName = "02_Review";
        scenario.PrimaryProject.State.LastInspectedImagePath = preferredImagePath;

        InspectorModeViewModel inspectorModeViewModel = new InspectorModeViewModel(
            scenario.TagFileService,
            scenario.TagDictionaryService,
            scenario.FileSystemService,
            scenario.ClipboardService,
            scenario.NavigationService,
            scenario.Messenger);
        inspectorModeViewModel.OnNavigatedTo(scenario.PrimaryProject);

        MainWindow window = scenario.CreateMainWindow(inspectorModeViewModel);
        await CaptureWindowAsync(window, outputFolder, "inspector-mode.png", 500).ConfigureAwait(true);
    }

    private static async Task CaptureWindowAsync(Window window, string outputFolder, string fileName, int delayMs)
    {
        window.Show();
        await Task.Delay(delayMs).ConfigureAwait(true);

        WriteableBitmap? bitmap = window.CaptureRenderedFrame();
        Assert.That(bitmap, Is.Not.Null, $"Expected a rendered frame for {fileName}.");

        string outputPath = TestOutputHelper.GetOutputPath(outputFolder, fileName);
        bitmap!.Save(outputPath);

        window.Close();
        await Task.Delay(50).ConfigureAwait(true);
    }

    private sealed class CaptureScenario : IDisposable
    {
        private CaptureScenario(
            string workspacePath,
            Project primaryProject,
            Project secondaryProject,
            StrongReferenceMessenger messenger,
            FileSystemService fileSystemService,
            TagFileService tagFileService,
            CaptureProjectService projectService,
            TagDictionaryService tagDictionaryService,
            CaptureAiTaggerService aiTaggerService,
            CaptureThumbnailCacheService thumbnailCacheService,
            CaptureClipboardService clipboardService,
            CaptureNavigationService navigationService)
        {
            WorkspacePath = workspacePath;
            PrimaryProject = primaryProject;
            SecondaryProject = secondaryProject;
            Messenger = messenger;
            FileSystemService = fileSystemService;
            TagFileService = tagFileService;
            ProjectService = projectService;
            TagDictionaryService = tagDictionaryService;
            AiTaggerService = aiTaggerService;
            ThumbnailCacheService = thumbnailCacheService;
            ClipboardService = clipboardService;
            NavigationService = navigationService;
        }

        public string WorkspacePath { get; }

        public Project PrimaryProject { get; }

        public Project SecondaryProject { get; }

        public StrongReferenceMessenger Messenger { get; }

        public FileSystemService FileSystemService { get; }

        public TagFileService TagFileService { get; }

        public CaptureProjectService ProjectService { get; }

        public TagDictionaryService TagDictionaryService { get; }

        public CaptureAiTaggerService AiTaggerService { get; }

        public CaptureThumbnailCacheService ThumbnailCacheService { get; }

        public CaptureClipboardService ClipboardService { get; }

        public CaptureNavigationService NavigationService { get; }

        public static async Task<CaptureScenario> CreateAsync()
        {
            string workspacePath = Path.Combine(Path.GetTempPath(), "DatasetStudioHeadless", Guid.NewGuid().ToString("N"));
            string masterRootPath = Path.Combine(workspacePath, "master-root");
            Directory.CreateDirectory(masterRootPath);

            Project primaryProject = CreateProject(
                masterRootPath,
                "Animal Study",
                "animal-study",
                "wd-eva02-large",
                new[] { "dataset", "clean" },
                new[]
                {
                    new WorkflowStage { Order = 0, FolderName = "01_Inbox", DisplayName = "Inbox" },
                    new WorkflowStage { Order = 1, FolderName = "02_Review", DisplayName = "Review" },
                },
                new[]
                {
                    new TagDictionaryEntry
                    {
                        CanonicalName = "feline",
                        Aliases = new List<string> { "cat" },
                    },
                    new TagDictionaryEntry
                    {
                        CanonicalName = "canine",
                        Aliases = new List<string> { "doggo" },
                    },
                    new TagDictionaryEntry
                    {
                        CanonicalName = "backlit",
                        Aliases = new List<string>(),
                    },
                },
                "02_Review",
                176);

            Project secondaryProject = CreateProject(
                masterRootPath,
                "Portrait Batch",
                "portrait-batch",
                "convnext-v2",
                new[] { "portrait" },
                new[]
                {
                    new WorkflowStage { Order = 0, FolderName = "01_Selects", DisplayName = "Selects" },
                },
                new[]
                {
                    new TagDictionaryEntry
                    {
                        CanonicalName = "studio",
                        Aliases = new List<string> { "indoors" },
                    },
                },
                "01_Selects",
                160);

            await SeedProjectFilesAsync(primaryProject, new Dictionary<string, IReadOnlyList<string>>
            {
                { Path.Combine(primaryProject.RootFolderPath, "01_Inbox", "fox.png"), new[] { "backlit" } },
                { Path.Combine(primaryProject.RootFolderPath, "02_Review", "cat.png"), new[] { "cat", "orange", "backlit" } },
                { Path.Combine(primaryProject.RootFolderPath, "02_Review", "dog.png"), new[] { "doggo", "studio" } },
            }).ConfigureAwait(false);

            await SeedProjectFilesAsync(secondaryProject, new Dictionary<string, IReadOnlyList<string>>
            {
                { Path.Combine(secondaryProject.RootFolderPath, "01_Selects", "portrait.png"), Array.Empty<string>() },
            }).ConfigureAwait(false);

            StrongReferenceMessenger messenger = new StrongReferenceMessenger();
            FileSystemService fileSystemService = new FileSystemService();
            TagFileService tagFileService = new TagFileService();
            CaptureProjectService projectService = new CaptureProjectService(primaryProject, secondaryProject);
            TagDictionaryService tagDictionaryService = new TagDictionaryService(projectService, tagFileService, messenger);
            CaptureAiTaggerService aiTaggerService = new CaptureAiTaggerService();
            CaptureThumbnailCacheService thumbnailCacheService = new CaptureThumbnailCacheService();
            CaptureClipboardService clipboardService = new CaptureClipboardService();
            CaptureNavigationService navigationService = new CaptureNavigationService();

            return new CaptureScenario(
                workspacePath,
                primaryProject,
                secondaryProject,
                messenger,
                fileSystemService,
                tagFileService,
                projectService,
                tagDictionaryService,
                aiTaggerService,
                thumbnailCacheService,
                clipboardService,
                navigationService);
        }

        public MainWindow CreateMainWindow(ScreenViewModelBase currentView)
        {
            MainWindowViewModel mainWindowViewModel = CreateMainWindowViewModel();
            mainWindowViewModel.CurrentView = currentView;

            return new MainWindow(mainWindowViewModel)
            {
                Width = 1440,
                Height = 900,
            };
        }

        public MainWindowViewModel CreateMainWindowViewModel()
        {
            ServiceCollection services = new ServiceCollection();
            IServiceProvider serviceProvider = services.BuildServiceProvider();
            return new MainWindowViewModel(serviceProvider, NavigationService, Messenger);
        }

        public void Dispose()
        {
            if (Directory.Exists(WorkspacePath))
            {
                Directory.Delete(WorkspacePath, true);
            }
        }

        private static Project CreateProject(
            string masterRootPath,
            string name,
            string projectId,
            string aiModelName,
            IReadOnlyList<string> prefixTags,
            IReadOnlyList<WorkflowStage> stages,
            IReadOnlyList<TagDictionaryEntry> dictionaryEntries,
            string activeStageFolderName,
            int zoomValue)
        {
            string rootFolderPath = Path.Combine(masterRootPath, projectId);
            Directory.CreateDirectory(rootFolderPath);

            foreach (WorkflowStage stage in stages)
            {
                Directory.CreateDirectory(Path.Combine(rootFolderPath, stage.FolderName));
            }

            return new Project
            {
                Id = projectId,
                Name = name,
                RootFolderPath = rootFolderPath,
                PrefixTags = prefixTags.ToList(),
                AiModelName = aiModelName,
                Stages = stages.ToList(),
                TagDictionaryEntries = dictionaryEntries.ToList(),
                LastModified = DateTime.UtcNow,
                State = new ProjectState
                {
                    ActiveStageFolderName = activeStageFolderName,
                    SelectedAiModelName = aiModelName,
                    ZoomSliderValue = zoomValue,
                },
            };
        }

        private static async Task SeedProjectFilesAsync(Project project, IReadOnlyDictionary<string, IReadOnlyList<string>> tagsByImagePath)
        {
            foreach (KeyValuePair<string, IReadOnlyList<string>> entry in tagsByImagePath)
            {
                string imagePath = entry.Key;
                string imageDirectory = Path.GetDirectoryName(imagePath) ?? project.RootFolderPath;
                Directory.CreateDirectory(imageDirectory);
                await File.WriteAllBytesAsync(imagePath, new byte[] { 0x00 }).ConfigureAwait(false);

                string tagFilePath = Path.ChangeExtension(imagePath, ".txt");
                string tagContents = string.Join(", ", entry.Value);
                await File.WriteAllTextAsync(tagFilePath, tagContents).ConfigureAwait(false);
            }
        }
    }

    private sealed class CaptureProjectService : IProjectService
    {
        private readonly List<Project> projects;

        public CaptureProjectService(params Project[] projects)
        {
            this.projects = projects.ToList();
        }

        public Task<IReadOnlyList<Project>> LoadProjectsAsync()
        {
            return Task.FromResult<IReadOnlyList<Project>>(projects.Select(CloneProject).ToList());
        }

        public Task<Project> CreateProjectAsync(string name, string rootFolder)
        {
            Project project = new Project
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = name,
                RootFolderPath = rootFolder,
                LastModified = DateTime.UtcNow,
            };

            projects.Add(project);
            return Task.FromResult(CloneProject(project));
        }

        public Task SaveProjectAsync(Project project)
        {
            int existingIndex = projects.FindIndex(candidate => string.Equals(candidate.Id, project.Id, StringComparison.OrdinalIgnoreCase));

            if (existingIndex >= 0)
            {
                projects[existingIndex] = CloneProject(project);
            }
            else
            {
                projects.Add(CloneProject(project));
            }

            return Task.CompletedTask;
        }

        public Task DeleteProjectAsync(string projectId)
        {
            projects.RemoveAll(project => string.Equals(project.Id, projectId, StringComparison.OrdinalIgnoreCase));
            return Task.CompletedTask;
        }

        private static Project CloneProject(Project project)
        {
            return new Project
            {
                Id = project.Id,
                Name = project.Name,
                RootFolderPath = project.RootFolderPath,
                AiModelName = project.AiModelName,
                LastModified = project.LastModified,
                PrefixTags = project.PrefixTags.ToList(),
                Stages = project.Stages
                    .Select(stage => new WorkflowStage
                    {
                        Order = stage.Order,
                        FolderName = stage.FolderName,
                        DisplayName = stage.DisplayName,
                    })
                    .ToList(),
                TagDictionaryEntries = project.TagDictionaryEntries
                    .Select(entry => new TagDictionaryEntry
                    {
                        CanonicalName = entry.CanonicalName,
                        Aliases = entry.Aliases.ToList(),
                        GlobalFrequency = entry.GlobalFrequency,
                    })
                    .ToList(),
                State = new ProjectState
                {
                    ActiveStageFolderName = project.State.ActiveStageFolderName,
                    ZoomSliderValue = project.State.ZoomSliderValue,
                    SelectedAiModelName = project.State.SelectedAiModelName,
                    LastInspectedImagePath = project.State.LastInspectedImagePath,
                },
            };
        }
    }

    private sealed class CaptureAiTaggerService : IAiTaggerService
    {
        public event EventHandler<AiTaggingCompletedMessage>? TagGenerationCompleted;

        public Task<IReadOnlyList<string>> GenerateTagsAsync(string imageFilePath, string modelName)
        {
            TagGenerationCompleted?.Invoke(this, new AiTaggingCompletedMessage(imageFilePath, new[] { "sample", modelName }));
            return Task.FromResult<IReadOnlyList<string>>(new[] { "sample", modelName });
        }

        public Task<IReadOnlyList<AiModelInfo>> GetAvailableModelsAsync()
        {
            IReadOnlyList<AiModelInfo> models = new[]
            {
                new AiModelInfo
                {
                    Id = "wd-eva02-large",
                    DisplayName = "WD EVA02 Large",
                    ModelPath = "models/wd-eva02-large.onnx",
                },
                new AiModelInfo
                {
                    Id = "convnext-v2",
                    DisplayName = "ConvNext V2",
                    ModelPath = "models/convnext-v2.onnx",
                },
            };

            return Task.FromResult(models);
        }

        public bool IsProcessing(string imageFilePath)
        {
            _ = imageFilePath;
            return false;
        }
    }

    private sealed class CaptureThumbnailCacheService : IThumbnailCacheService
    {
        public Task<Stream> GetThumbnailAsync(string imageFilePath, int size)
        {
            Avalonia.Media.Color fillColor = imageFilePath.GetHashCode(StringComparison.OrdinalIgnoreCase) % 2 == 0
                ? Avalonia.Media.Color.Parse("#D79921")
                : Avalonia.Media.Color.Parse("#458588");

            using Image<Rgba32> image = new Image<Rgba32>(size, size, new Rgba32(fillColor.R, fillColor.G, fillColor.B, fillColor.A));
            MemoryStream memoryStream = new MemoryStream();
            image.Save(memoryStream, new PngEncoder());
            memoryStream.Position = 0;
            return Task.FromResult<Stream>(memoryStream);
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

    private sealed class CaptureClipboardService : IClipboardService
    {
        public IReadOnlyList<string> LastCopiedTags { get; private set; } = Array.Empty<string>();

        public IReadOnlyList<string> PasteTagsResult { get; set; } = Array.Empty<string>();

        public Task CopyTagsAsync(IReadOnlyList<string> tags)
        {
            LastCopiedTags = tags.ToList();
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<string>> PasteTagsAsync()
        {
            return Task.FromResult(PasteTagsResult);
        }
    }

    private sealed class CaptureNavigationService : INavigationService
    {
        public int GoBackCount { get; private set; }

        public void NavigateTo<TViewModel>() where TViewModel : ScreenViewModelBase
        {
        }

        public void NavigateTo<TViewModel>(object parameter) where TViewModel : ScreenViewModelBase
        {
            _ = parameter;
        }

        public void GoBack()
        {
            GoBackCount++;
        }
    }
}
