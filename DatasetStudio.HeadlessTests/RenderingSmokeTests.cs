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
        await CaptureTagsOverviewAsync(scenario, outputFolder).ConfigureAwait(true);
        await CaptureProjectOverviewAsync(scenario, outputFolder).ConfigureAwait(true);
        await CaptureProjectOverviewBatchAddPopupAsync(scenario, outputFolder).ConfigureAwait(true);
        await CaptureProjectOverviewBatchRemovePopupAsync(scenario, outputFolder).ConfigureAwait(true);
        await CaptureProjectOverviewAiProcessingAsync(scenario, outputFolder).ConfigureAwait(true);
        await CaptureInspectorModeAsync(scenario, outputFolder).ConfigureAwait(true);
        await CaptureInspectorSuggestionsAsync(scenario, outputFolder).ConfigureAwait(true);

        Assert.That(File.Exists(Path.Combine(outputFolder, "projects-hub.png")), Is.True);
        Assert.That(File.Exists(Path.Combine(outputFolder, "project-configuration.png")), Is.True);
        Assert.That(File.Exists(Path.Combine(outputFolder, "tags-overview.png")), Is.True);
        Assert.That(File.Exists(Path.Combine(outputFolder, "project-overview.png")), Is.True);
        Assert.That(File.Exists(Path.Combine(outputFolder, "project-overview-batch-add.png")), Is.True);
        Assert.That(File.Exists(Path.Combine(outputFolder, "project-overview-batch-remove.png")), Is.True);
        Assert.That(File.Exists(Path.Combine(outputFolder, "project-overview-ai-processing.png")), Is.True);
        Assert.That(File.Exists(Path.Combine(outputFolder, "inspector-mode.png")), Is.True);
        Assert.That(File.Exists(Path.Combine(outputFolder, "inspector-suggestions.png")), Is.True);
    }

    [AvaloniaTest]
    public async Task ProjectOverviewSlashShortcutFocusesFilterTextBox()
    {
        using CaptureScenario scenario = await CaptureScenario.CreateAsync().ConfigureAwait(true);
        ProjectOverviewViewModel projectOverviewViewModel = new ProjectOverviewViewModel(
            scenario.FileSystemService,
            scenario.TagFileService,
            scenario.TagDictionaryService,
            scenario.ThumbnailCacheService,
            scenario.ClipboardService,
            scenario.NavigationService,
            scenario.AiTaggerService,
            new BatchTagOperationService(scenario.TagFileService, scenario.TagDictionaryService, scenario.Messenger),
            scenario.Messenger,
            scenario.StatePersistenceService);
        projectOverviewViewModel.OnNavigatedTo(scenario.PrimaryProject);

        MainWindow window = scenario.CreateMainWindow(projectOverviewViewModel);
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
    public async Task ProjectOverviewGlobalCopyShortcutCopiesFocusedImageTags()
    {
        using CaptureScenario scenario = await CaptureScenario.CreateAsync().ConfigureAwait(true);
        ProjectOverviewViewModel projectOverviewViewModel = new ProjectOverviewViewModel(
            scenario.FileSystemService,
            scenario.TagFileService,
            scenario.TagDictionaryService,
            scenario.ThumbnailCacheService,
            scenario.ClipboardService,
            scenario.NavigationService,
            scenario.AiTaggerService,
            new BatchTagOperationService(scenario.TagFileService, scenario.TagDictionaryService, scenario.Messenger),
            scenario.Messenger,
            scenario.StatePersistenceService);
        projectOverviewViewModel.OnNavigatedTo(scenario.PrimaryProject);

        MainWindow window = scenario.CreateMainWindow(projectOverviewViewModel);
        window.Show();
        await Task.Delay(500).ConfigureAwait(true);

        IReadOnlyList<string> expectedTags = projectOverviewViewModel.Images[projectOverviewViewModel.FocusedImageIndex].Tags;
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
            scenario.AiTaggerService,
            scenario.Messenger,
            scenario.StatePersistenceService);
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

    [AvaloniaTest]
    public async Task ProjectsHubCtrlNShortcutCreatesProjectAndOpensConfigurationModal()
    {
        using CaptureScenario scenario = await CaptureScenario.CreateAsync().ConfigureAwait(true);
        ProjectsHubViewModel projectsHubViewModel = new ProjectsHubViewModel(
            scenario.ProjectService,
            scenario.FileSystemService,
            scenario.NavigationService,
            scenario.Messenger,
            scenario.StatePersistenceService);

        DirectoryInfo? masterRootDirectory = Directory.GetParent(scenario.PrimaryProject.RootFolderPath);
        Assert.That(masterRootDirectory, Is.Not.Null);
        string masterRootPath = masterRootDirectory.FullName;
        projectsHubViewModel.MasterRootPath = masterRootPath;

        ServiceCollection services = new ServiceCollection();
        services.AddSingleton<IMessenger>(scenario.Messenger);
        services.AddSingleton<IProjectService>(scenario.ProjectService);
        services.AddSingleton<IAiTaggerService>(scenario.AiTaggerService);
        services.AddSingleton<IFileSystemService>(scenario.FileSystemService);
        services.AddTransient<ProjectConfigurationViewModel>();
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        MainWindowViewModel mainWindowViewModel = new MainWindowViewModel(serviceProvider, scenario.NavigationService, scenario.Messenger)
        {
            CurrentView = projectsHubViewModel,
        };

        MainWindow window = new MainWindow(mainWindowViewModel)
        {
            Width = 1440,
            Height = 900,
        };
        window.Show();
        await Task.Delay(300).ConfigureAwait(true);

        bool didFocusWindow = window.Focus();
        _ = didFocusWindow;
        window.KeyPress(Key.N, RawInputModifiers.Control, PhysicalKey.N, "n");
        await WaitForConditionAsync(() => mainWindowViewModel.IsConfigOpen).ConfigureAwait(true);

        IReadOnlyList<Project> projects = await scenario.ProjectService.LoadProjectsAsync().ConfigureAwait(true);
        Assert.That(projects.Count, Is.EqualTo(3));
        Assert.That(mainWindowViewModel.ProjectConfigurationContent, Is.TypeOf<ProjectConfigurationViewModel>());

        window.Close();
        serviceProvider.Dispose();
        projectsHubViewModel.Dispose();
    }

    [AvaloniaTest]
    public async Task ProjectConfigurationCtrlSSavesProjectAndClosesModal()
    {
        using CaptureScenario scenario = await CaptureScenario.CreateAsync().ConfigureAwait(true);
        ProjectsHubViewModel projectsHubViewModel = new ProjectsHubViewModel(
            scenario.ProjectService,
            scenario.FileSystemService,
            scenario.NavigationService,
            scenario.Messenger,
            scenario.StatePersistenceService);
        ProjectConfigurationViewModel projectConfigurationViewModel = new ProjectConfigurationViewModel(
            scenario.ProjectService,
            scenario.AiTaggerService,
            scenario.FileSystemService,
            scenario.Messenger);
        projectConfigurationViewModel.LoadProject(scenario.PrimaryProject);
        projectConfigurationViewModel.ProjectName = "Animal Study Revised";

        MainWindowViewModel mainWindowViewModel = scenario.CreateMainWindowViewModel();
        mainWindowViewModel.CurrentView = projectsHubViewModel;
        mainWindowViewModel.OpenProjectConfiguration(projectConfigurationViewModel);

        MainWindow window = new MainWindow(mainWindowViewModel)
        {
            Width = 1440,
            Height = 900,
        };

        window.Show();
        await Task.Delay(450).ConfigureAwait(true);

        TextBox? projectNameTextBox = FindDescendantByName<TextBox>(window, "ProjectNameTextBox");
        Assert.That(projectNameTextBox, Is.Not.Null);

        bool didFocusProjectNameTextBox = projectNameTextBox.Focus();
        _ = didFocusProjectNameTextBox;
        window.KeyPress(Key.S, RawInputModifiers.Control, PhysicalKey.S, "s");
        await WaitForConditionAsync(() => !mainWindowViewModel.IsConfigOpen).ConfigureAwait(true);

        IReadOnlyList<Project> savedProjects = await scenario.ProjectService.LoadProjectsAsync().ConfigureAwait(true);
        Project? savedProject = savedProjects.SingleOrDefault(project => string.Equals(project.Id, scenario.PrimaryProject.Id, StringComparison.Ordinal));
        Assert.That(savedProject, Is.Not.Null);
        Assert.That(savedProject!.Name, Is.EqualTo("Animal Study Revised"));

        window.Close();
        projectsHubViewModel.Dispose();
    }

    [AvaloniaTest]
    public async Task TagsOverviewSlashShortcutFocusesSearchTextBox()
    {
        using CaptureScenario scenario = await CaptureScenario.CreateAsync().ConfigureAwait(true);
        TagsOverviewViewModel tagsOverviewViewModel = new TagsOverviewViewModel(
            scenario.TagDictionaryService,
            scenario.Messenger);
        tagsOverviewViewModel.OnNavigatedTo(scenario.PrimaryProject.Id);

        MainWindow window = scenario.CreateMainWindow(tagsOverviewViewModel);
        window.Show();
        await Task.Delay(350).ConfigureAwait(true);

        TextBox? searchTextBox = FindDescendantByName<TextBox>(window, "SearchTextBox");
        Assert.That(searchTextBox, Is.Not.Null);

        bool didFocusWindow = window.Focus();
        _ = didFocusWindow;
        window.KeyPress(Key.Oem2, RawInputModifiers.None, PhysicalKey.Slash, "/");
        await Task.Delay(50).ConfigureAwait(true);

        Assert.That(searchTextBox!.IsKeyboardFocusWithin, Is.True);

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

    private static async Task WaitForConditionAsync(Func<bool> condition)
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(25).ConfigureAwait(true);
        }

        Assert.Fail("Condition was not met within the allotted time.");
    }

    private static async Task CaptureProjectsHubAsync(CaptureScenario scenario, string outputFolder)
    {
        ProjectsHubViewModel projectsHubViewModel = new ProjectsHubViewModel(
            scenario.ProjectService,
            scenario.FileSystemService,
            scenario.NavigationService,
            scenario.Messenger,
            scenario.StatePersistenceService);

        MainWindow window = scenario.CreateMainWindow(projectsHubViewModel);
        await CaptureWindowAsync(window, outputFolder, "projects-hub.png", 350).ConfigureAwait(true);
    }

    private static async Task CaptureProjectConfigurationAsync(CaptureScenario scenario, string outputFolder)
    {
        ProjectsHubViewModel projectsHubViewModel = new ProjectsHubViewModel(
            scenario.ProjectService,
            scenario.FileSystemService,
            scenario.NavigationService,
            scenario.Messenger,
            scenario.StatePersistenceService);

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

    private static async Task CaptureTagsOverviewAsync(CaptureScenario scenario, string outputFolder)
    {
        TagsOverviewViewModel tagsOverviewViewModel = new TagsOverviewViewModel(
            scenario.TagDictionaryService,
            scenario.Messenger);
        tagsOverviewViewModel.OnNavigatedTo(scenario.PrimaryProject.Id);

        MainWindow window = scenario.CreateMainWindow(tagsOverviewViewModel);
        await CaptureWindowAsync(window, outputFolder, "tags-overview.png", 350).ConfigureAwait(true);
    }

    private static async Task CaptureProjectOverviewAsync(CaptureScenario scenario, string outputFolder)
    {
        ProjectOverviewViewModel projectOverviewViewModel = new ProjectOverviewViewModel(
            scenario.FileSystemService,
            scenario.TagFileService,
            scenario.TagDictionaryService,
            scenario.ThumbnailCacheService,
            scenario.ClipboardService,
            scenario.NavigationService,
            scenario.AiTaggerService,
            new BatchTagOperationService(scenario.TagFileService, scenario.TagDictionaryService, scenario.Messenger),
            scenario.Messenger,
            scenario.StatePersistenceService);
        projectOverviewViewModel.OnNavigatedTo(scenario.PrimaryProject);

        MainWindow window = scenario.CreateMainWindow(projectOverviewViewModel);
        await CaptureWindowAsync(window, outputFolder, "project-overview.png", 500).ConfigureAwait(true);
    }

    private static async Task CaptureProjectOverviewBatchAddPopupAsync(CaptureScenario scenario, string outputFolder)
    {
        ProjectOverviewViewModel projectOverviewViewModel = new ProjectOverviewViewModel(
            scenario.FileSystemService,
            scenario.TagFileService,
            scenario.TagDictionaryService,
            scenario.ThumbnailCacheService,
            scenario.ClipboardService,
            scenario.NavigationService,
            scenario.AiTaggerService,
            new BatchTagOperationService(scenario.TagFileService, scenario.TagDictionaryService, scenario.Messenger),
            scenario.Messenger,
            scenario.StatePersistenceService);
        projectOverviewViewModel.OnNavigatedTo(scenario.PrimaryProject);

        MainWindow window = scenario.CreateMainWindow(projectOverviewViewModel);
        window.Show();
        await Task.Delay(500).ConfigureAwait(true);

        projectOverviewViewModel.OpenBatchAddCommand.Execute(null);
        await WaitForConditionAsync(() => projectOverviewViewModel.IsBatchAddOpen).ConfigureAwait(true);
        Assert.That(projectOverviewViewModel.IsBatchAddOpen, Is.True);
        await CaptureOpenWindowAsync(window, outputFolder, "project-overview-batch-add.png").ConfigureAwait(true);
    }

    private static async Task CaptureProjectOverviewBatchRemovePopupAsync(CaptureScenario scenario, string outputFolder)
    {
        ProjectOverviewViewModel projectOverviewViewModel = new ProjectOverviewViewModel(
            scenario.FileSystemService,
            scenario.TagFileService,
            scenario.TagDictionaryService,
            scenario.ThumbnailCacheService,
            scenario.ClipboardService,
            scenario.NavigationService,
            scenario.AiTaggerService,
            new BatchTagOperationService(scenario.TagFileService, scenario.TagDictionaryService, scenario.Messenger),
            scenario.Messenger,
            scenario.StatePersistenceService);
        projectOverviewViewModel.OnNavigatedTo(scenario.PrimaryProject);

        MainWindow window = scenario.CreateMainWindow(projectOverviewViewModel);
        window.Show();
        await Task.Delay(500).ConfigureAwait(true);

        projectOverviewViewModel.OpenBatchRemoveCommand.Execute(null);
        await WaitForConditionAsync(() => projectOverviewViewModel.IsBatchRemoveOpen).ConfigureAwait(true);
        Assert.That(projectOverviewViewModel.IsBatchRemoveOpen, Is.True);
        await CaptureOpenWindowAsync(window, outputFolder, "project-overview-batch-remove.png").ConfigureAwait(true);
    }

    private static async Task CaptureProjectOverviewAiProcessingAsync(CaptureScenario scenario, string outputFolder)
    {
        ProjectOverviewViewModel projectOverviewViewModel = new ProjectOverviewViewModel(
            scenario.FileSystemService,
            scenario.TagFileService,
            scenario.TagDictionaryService,
            scenario.ThumbnailCacheService,
            scenario.ClipboardService,
            scenario.NavigationService,
            scenario.AiTaggerService,
            new BatchTagOperationService(scenario.TagFileService, scenario.TagDictionaryService, scenario.Messenger),
            scenario.Messenger,
            scenario.StatePersistenceService);
        projectOverviewViewModel.OnNavigatedTo(scenario.PrimaryProject);

        MainWindow window = scenario.CreateMainWindow(projectOverviewViewModel);
        window.Show();
        await Task.Delay(500).ConfigureAwait(true);

        Assert.That(projectOverviewViewModel.Images.Count, Is.GreaterThan(0));
        projectOverviewViewModel.Images[0].IsAiProcessing = true;
        Assert.That(projectOverviewViewModel.Images[0].IsAiProcessing, Is.True);
        await Task.Delay(75).ConfigureAwait(true);
        await CaptureOpenWindowAsync(window, outputFolder, "project-overview-ai-processing.png").ConfigureAwait(true);
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
            scenario.AiTaggerService,
            scenario.Messenger,
            scenario.StatePersistenceService);
        inspectorModeViewModel.OnNavigatedTo(scenario.PrimaryProject);

        MainWindow window = scenario.CreateMainWindow(inspectorModeViewModel);
        await CaptureWindowAsync(window, outputFolder, "inspector-mode.png", 500).ConfigureAwait(true);
    }

    private static async Task CaptureInspectorSuggestionsAsync(CaptureScenario scenario, string outputFolder)
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
            scenario.AiTaggerService,
            scenario.Messenger,
            scenario.StatePersistenceService);
        inspectorModeViewModel.OnNavigatedTo(scenario.PrimaryProject);

        MainWindow window = scenario.CreateMainWindow(inspectorModeViewModel);
        window.Show();
        await Task.Delay(500).ConfigureAwait(true);

        await WaitForConditionAsync(() => inspectorModeViewModel.CurrentImage is not null).ConfigureAwait(true);

        inspectorModeViewModel.TagInputText = "d";
        await WaitForConditionAsync(() => inspectorModeViewModel.IsSuggestOpen).ConfigureAwait(true);
        Assert.That(inspectorModeViewModel.IsSuggestOpen, Is.True);
        Assert.That(inspectorModeViewModel.AutoSuggestTags, Is.Not.Empty);
        await CaptureOpenWindowAsync(window, outputFolder, "inspector-suggestions.png").ConfigureAwait(true);
    }

    private static async Task CaptureWindowAsync(Window window, string outputFolder, string fileName, int delayMs)
    {
        window.Show();
        await Task.Delay(delayMs).ConfigureAwait(true);

        await CaptureOpenWindowAsync(window, outputFolder, fileName).ConfigureAwait(true);

        window.Close();
        await Task.Delay(50).ConfigureAwait(true);
    }

    private static Task CaptureOpenWindowAsync(Window window, string outputFolder, string fileName)
    {
        WriteableBitmap? bitmap = window.CaptureRenderedFrame();
        Assert.That(bitmap, Is.Not.Null, $"Expected a rendered frame for {fileName}.");

        string outputPath = TestOutputHelper.GetOutputPath(outputFolder, fileName);
        bitmap!.Save(outputPath);
        return Task.CompletedTask;
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
            CaptureNavigationService navigationService,
            CaptureStatePersistenceService statePersistenceService)
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
            StatePersistenceService = statePersistenceService;
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

        public CaptureStatePersistenceService StatePersistenceService { get; }

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
            CaptureStatePersistenceService statePersistenceService = new CaptureStatePersistenceService();
            statePersistenceService.SetAppState(new AppState
            {
                LastMasterRootDirectory = masterRootPath,
                LastOpenedProjectId = primaryProject.Id,
            });
            statePersistenceService.SetProjectState(primaryProject.Id, primaryProject.State);
            statePersistenceService.SetProjectState(secondaryProject.Id, secondaryProject.State);

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
                navigationService,
                statePersistenceService);
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
                byte[] sampleImageBytes = CreateSampleImageBytes(imagePath, 768);
                await File.WriteAllBytesAsync(imagePath, sampleImageBytes).ConfigureAwait(false);

                string tagFilePath = Path.ChangeExtension(imagePath, ".txt");
                string tagContents = string.Join(", ", entry.Value);
                await File.WriteAllTextAsync(tagFilePath, tagContents).ConfigureAwait(false);
            }
        }
    }

    private static byte[] CreateSampleImageBytes(string imageFilePath, int size)
    {
        string imageName = Path.GetFileNameWithoutExtension(imageFilePath);

        using Image<Rgba32> image = new Image<Rgba32>(size, size);

        if (string.Equals(imageName, "cat", StringComparison.OrdinalIgnoreCase))
        {
            RenderCatSample(image);
        }
        else if (string.Equals(imageName, "dog", StringComparison.OrdinalIgnoreCase))
        {
            RenderDogSample(image);
        }
        else if (string.Equals(imageName, "fox", StringComparison.OrdinalIgnoreCase))
        {
            RenderFoxSample(image);
        }
        else if (string.Equals(imageName, "portrait", StringComparison.OrdinalIgnoreCase))
        {
            RenderPortraitSample(image);
        }
        else
        {
            RenderGenericSample(image, imageName);
        }

        MemoryStream memoryStream = new MemoryStream();
        image.Save(memoryStream, new PngEncoder());
        return memoryStream.ToArray();
    }

    private static void RenderCatSample(Image<Rgba32> image)
    {
        int size = image.Width;
        FillImage(image, new Rgba32(250, 241, 214, 255));
        FillRect(image, 0, (size * 3) / 4, size, size / 4, new Rgba32(228, 209, 170, 255));

        int headCenterX = size / 2;
        int headCenterY = (size * 11) / 24;
        int headRadius = size / 4;

        FillTriangle(image, headCenterX - headRadius, headCenterY - headRadius / 3, headCenterX - headRadius / 3, headCenterY - headRadius - headRadius / 2, headCenterX - headRadius / 10, headCenterY - headRadius / 5, new Rgba32(219, 141, 65, 255));
        FillTriangle(image, headCenterX + headRadius / 10, headCenterY - headRadius / 5, headCenterX + headRadius / 3, headCenterY - headRadius - headRadius / 2, headCenterX + headRadius, headCenterY - headRadius / 3, new Rgba32(219, 141, 65, 255));
        FillCircle(image, headCenterX, headCenterY, headRadius, new Rgba32(232, 154, 77, 255));

        FillTriangle(image, headCenterX - headRadius + headRadius / 5, headCenterY - headRadius / 2, headCenterX - headRadius / 3, headCenterY - headRadius - headRadius / 3, headCenterX - headRadius / 16, headCenterY - headRadius / 5, new Rgba32(248, 202, 187, 255));
        FillTriangle(image, headCenterX + headRadius / 16, headCenterY - headRadius / 5, headCenterX + headRadius / 3, headCenterY - headRadius - headRadius / 3, headCenterX + headRadius - headRadius / 5, headCenterY - headRadius / 2, new Rgba32(248, 202, 187, 255));

        FillCircle(image, headCenterX - headRadius / 2, headCenterY - headRadius / 4, headRadius / 6, new Rgba32(248, 245, 239, 255));
        FillCircle(image, headCenterX + headRadius / 2, headCenterY - headRadius / 4, headRadius / 6, new Rgba32(248, 245, 239, 255));
        FillCircle(image, headCenterX - headRadius / 2, headCenterY - headRadius / 4, headRadius / 12, new Rgba32(50, 42, 39, 255));
        FillCircle(image, headCenterX + headRadius / 2, headCenterY - headRadius / 4, headRadius / 12, new Rgba32(50, 42, 39, 255));

        FillCircle(image, headCenterX, headCenterY + headRadius / 6, headRadius / 4, new Rgba32(245, 229, 205, 255));
        FillTriangle(image, headCenterX - headRadius / 10, headCenterY + headRadius / 10, headCenterX + headRadius / 10, headCenterY + headRadius / 10, headCenterX, headCenterY + headRadius / 4, new Rgba32(197, 118, 122, 255));

        DrawLine(image, headCenterX - headRadius / 8, headCenterY + headRadius / 4, headCenterX - headRadius / 2, headCenterY + headRadius / 2, new Rgba32(89, 72, 55, 255), 3);
        DrawLine(image, headCenterX + headRadius / 8, headCenterY + headRadius / 4, headCenterX + headRadius / 2, headCenterY + headRadius / 2, new Rgba32(89, 72, 55, 255), 3);
        DrawLine(image, headCenterX - headRadius / 6, headCenterY + headRadius / 6, headCenterX - headRadius, headCenterY + headRadius / 7, new Rgba32(89, 72, 55, 255), 3);
        DrawLine(image, headCenterX - headRadius / 6, headCenterY + headRadius / 5, headCenterX - headRadius, headCenterY + headRadius / 3, new Rgba32(89, 72, 55, 255), 3);
        DrawLine(image, headCenterX + headRadius / 6, headCenterY + headRadius / 6, headCenterX + headRadius, headCenterY + headRadius / 7, new Rgba32(89, 72, 55, 255), 3);
        DrawLine(image, headCenterX + headRadius / 6, headCenterY + headRadius / 5, headCenterX + headRadius, headCenterY + headRadius / 3, new Rgba32(89, 72, 55, 255), 3);

        FillRect(image, headCenterX - headRadius / 6, headCenterY - headRadius / 12, headRadius / 8, headRadius / 2, new Rgba32(191, 119, 52, 255));
        FillRect(image, headCenterX + headRadius / 24, headCenterY - headRadius / 12, headRadius / 8, headRadius / 2, new Rgba32(191, 119, 52, 255));
    }

    private static void RenderDogSample(Image<Rgba32> image)
    {
        int size = image.Width;
        FillImage(image, new Rgba32(233, 240, 235, 255));
        FillRect(image, 0, (size * 3) / 4, size, size / 4, new Rgba32(206, 217, 193, 255));

        int headCenterX = size / 2;
        int headCenterY = (size * 11) / 24;
        int headRadius = size / 4;

        FillCircle(image, headCenterX - headRadius + headRadius / 5, headCenterY - headRadius / 10, headRadius / 2, new Rgba32(92, 67, 50, 255));
        FillCircle(image, headCenterX + headRadius - headRadius / 5, headCenterY - headRadius / 10, headRadius / 2, new Rgba32(92, 67, 50, 255));
        FillCircle(image, headCenterX, headCenterY, headRadius, new Rgba32(207, 171, 124, 255));
        FillCircle(image, headCenterX, headCenterY + headRadius / 4, headRadius / 3, new Rgba32(242, 225, 199, 255));

        FillCircle(image, headCenterX - headRadius / 2, headCenterY - headRadius / 5, headRadius / 8, new Rgba32(50, 42, 39, 255));
        FillCircle(image, headCenterX + headRadius / 2, headCenterY - headRadius / 5, headRadius / 8, new Rgba32(50, 42, 39, 255));
        FillCircle(image, headCenterX, headCenterY + headRadius / 6, headRadius / 10, new Rgba32(45, 37, 34, 255));
        DrawLine(image, headCenterX, headCenterY + headRadius / 6, headCenterX, headCenterY + headRadius / 3, new Rgba32(45, 37, 34, 255), 3);
        DrawLine(image, headCenterX, headCenterY + headRadius / 3, headCenterX - headRadius / 8, headCenterY + headRadius / 2, new Rgba32(45, 37, 34, 255), 3);
        DrawLine(image, headCenterX, headCenterY + headRadius / 3, headCenterX + headRadius / 8, headCenterY + headRadius / 2, new Rgba32(45, 37, 34, 255), 3);

        FillRect(image, headCenterX - headRadius / 6, headCenterY - headRadius / 12, headRadius / 7, headRadius / 2, new Rgba32(181, 135, 87, 255));
        FillRect(image, headCenterX + headRadius / 30, headCenterY - headRadius / 12, headRadius / 7, headRadius / 2, new Rgba32(181, 135, 87, 255));
        FillCircle(image, headCenterX, headCenterY + headRadius / 2 + headRadius / 10, headRadius / 9, new Rgba32(214, 114, 108, 255));
    }

    private static void RenderFoxSample(Image<Rgba32> image)
    {
        int size = image.Width;
        FillImage(image, new Rgba32(234, 245, 241, 255));
        FillRect(image, 0, (size * 7) / 10, size, size / 3, new Rgba32(192, 223, 214, 255));

        int headCenterX = size / 2;
        int headCenterY = size / 2;
        int headRadius = size / 4;

        FillTriangle(image, headCenterX - headRadius, headCenterY - headRadius / 3, headCenterX - headRadius / 3, headCenterY - headRadius - headRadius / 2, headCenterX - headRadius / 10, headCenterY - headRadius / 7, new Rgba32(212, 101, 43, 255));
        FillTriangle(image, headCenterX + headRadius / 10, headCenterY - headRadius / 7, headCenterX + headRadius / 3, headCenterY - headRadius - headRadius / 2, headCenterX + headRadius, headCenterY - headRadius / 3, new Rgba32(212, 101, 43, 255));
        FillTriangle(image, headCenterX - headRadius, headCenterY - headRadius / 4, headCenterX + headRadius, headCenterY - headRadius / 4, headCenterX, headCenterY + headRadius, new Rgba32(228, 114, 43, 255));
        FillTriangle(image, headCenterX - headRadius / 2, headCenterY + headRadius / 12, headCenterX + headRadius / 2, headCenterY + headRadius / 12, headCenterX, headCenterY + headRadius, new Rgba32(248, 240, 233, 255));
        FillCircle(image, headCenterX - headRadius / 3, headCenterY, headRadius / 10, new Rgba32(39, 36, 34, 255));
        FillCircle(image, headCenterX + headRadius / 3, headCenterY, headRadius / 10, new Rgba32(39, 36, 34, 255));
        FillTriangle(image, headCenterX - headRadius / 10, headCenterY + headRadius / 4, headCenterX + headRadius / 10, headCenterY + headRadius / 4, headCenterX, headCenterY + headRadius / 2, new Rgba32(56, 45, 41, 255));
    }

    private static void RenderPortraitSample(Image<Rgba32> image)
    {
        int size = image.Width;
        FillImage(image, new Rgba32(229, 222, 210, 255));
        FillRect(image, 0, 0, size, size / 3, new Rgba32(203, 180, 155, 255));
        FillRect(image, 0, (size * 2) / 3, size, size / 3, new Rgba32(126, 107, 92, 255));

        int headCenterX = size / 2;
        int headCenterY = size / 2 - size / 14;
        int headRadius = size / 6;

        FillCircle(image, headCenterX, headCenterY, headRadius, new Rgba32(222, 185, 151, 255));
        FillRect(image, headCenterX - headRadius, headCenterY + headRadius, headRadius * 2, size / 3, new Rgba32(87, 111, 132, 255));
        FillRect(image, headCenterX - headRadius / 2, headCenterY + headRadius / 2, headRadius, headRadius / 2, new Rgba32(222, 185, 151, 255));
        FillRect(image, headCenterX - headRadius - headRadius / 6, headCenterY - headRadius - headRadius / 2, headRadius * 2 + headRadius / 3, headRadius, new Rgba32(71, 53, 45, 255));
    }

    private static void RenderGenericSample(Image<Rgba32> image, string imageName)
    {
        _ = imageName;
        int size = image.Width;
        FillImage(image, new Rgba32(240, 234, 214, 255));
        FillRect(image, size / 8, size / 8, (size * 3) / 4, (size * 3) / 4, new Rgba32(177, 149, 89, 255));
        FillRect(image, size / 4, size / 4, size / 2, size / 2, new Rgba32(92, 133, 114, 255));
        DrawLine(image, size / 4, size / 4, (size * 3) / 4, (size * 3) / 4, new Rgba32(52, 48, 45, 255), 6);
        DrawLine(image, (size * 3) / 4, size / 4, size / 4, (size * 3) / 4, new Rgba32(52, 48, 45, 255), 6);
    }

    private static void FillImage(Image<Rgba32> image, Rgba32 color)
    {
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                image[x, y] = color;
            }
        }
    }

    private static void FillRect(Image<Rgba32> image, int left, int top, int width, int height, Rgba32 color)
    {
        int startX = Math.Max(0, left);
        int startY = Math.Max(0, top);
        int endX = Math.Min(image.Width, left + width);
        int endY = Math.Min(image.Height, top + height);

        for (int y = startY; y < endY; y++)
        {
            for (int x = startX; x < endX; x++)
            {
                image[x, y] = color;
            }
        }
    }

    private static void FillCircle(Image<Rgba32> image, int centerX, int centerY, int radius, Rgba32 color)
    {
        int squaredRadius = radius * radius;
        int startX = Math.Max(0, centerX - radius);
        int endX = Math.Min(image.Width - 1, centerX + radius);
        int startY = Math.Max(0, centerY - radius);
        int endY = Math.Min(image.Height - 1, centerY + radius);

        for (int y = startY; y <= endY; y++)
        {
            for (int x = startX; x <= endX; x++)
            {
                int deltaX = x - centerX;
                int deltaY = y - centerY;
                if ((deltaX * deltaX) + (deltaY * deltaY) <= squaredRadius)
                {
                    image[x, y] = color;
                }
            }
        }
    }

    private static void FillTriangle(Image<Rgba32> image, int x1, int y1, int x2, int y2, int x3, int y3, Rgba32 color)
    {
        int minX = Math.Max(0, Math.Min(x1, Math.Min(x2, x3)));
        int maxX = Math.Min(image.Width - 1, Math.Max(x1, Math.Max(x2, x3)));
        int minY = Math.Max(0, Math.Min(y1, Math.Min(y2, y3)));
        int maxY = Math.Min(image.Height - 1, Math.Max(y1, Math.Max(y2, y3)));

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                bool hasSameSign = HasSameSign(x, y, x1, y1, x2, y2, x3, y3);
                if (hasSameSign)
                {
                    image[x, y] = color;
                }
            }
        }
    }

    private static bool HasSameSign(int pointX, int pointY, int x1, int y1, int x2, int y2, int x3, int y3)
    {
        int d1 = CalculateEdgeSign(pointX, pointY, x1, y1, x2, y2);
        int d2 = CalculateEdgeSign(pointX, pointY, x2, y2, x3, y3);
        int d3 = CalculateEdgeSign(pointX, pointY, x3, y3, x1, y1);

        bool hasNegative = d1 < 0 || d2 < 0 || d3 < 0;
        bool hasPositive = d1 > 0 || d2 > 0 || d3 > 0;
        return !(hasNegative && hasPositive);
    }

    private static int CalculateEdgeSign(int pointX, int pointY, int x1, int y1, int x2, int y2)
    {
        return (pointX - x2) * (y1 - y2) - (x1 - x2) * (pointY - y2);
    }

    private static void DrawLine(Image<Rgba32> image, int startX, int startY, int endX, int endY, Rgba32 color, int thickness)
    {
        int deltaX = Math.Abs(endX - startX);
        int deltaY = Math.Abs(endY - startY);
        int stepX = startX < endX ? 1 : -1;
        int stepY = startY < endY ? 1 : -1;
        int error = deltaX - deltaY;
        int currentX = startX;
        int currentY = startY;
        int radius = Math.Max(1, thickness / 2);

        while (true)
        {
            FillCircle(image, currentX, currentY, radius, color);

            if (currentX == endX && currentY == endY)
            {
                break;
            }

            int doubledError = error * 2;
            if (doubledError > -deltaY)
            {
                error -= deltaY;
                currentX += stepX;
            }

            if (doubledError < deltaX)
            {
                error += deltaX;
                currentY += stepY;
            }
        }
    }

    private sealed class CaptureStatePersistenceService : IStatePersistenceService
    {
        private readonly Dictionary<string, ProjectState> projectStatesById = new Dictionary<string, ProjectState>(StringComparer.OrdinalIgnoreCase);
        private AppState appState = new AppState();

        public Task SaveAppStateAsync(AppState state)
        {
            appState = CloneAppState(state);
            return Task.CompletedTask;
        }

        public Task<AppState> LoadAppStateAsync()
        {
            return Task.FromResult(CloneAppState(appState));
        }

        public Task<AppState> UpdateAppStateAsync(Action<AppState> updateAction)
        {
            if (updateAction is null)
            {
                throw new ArgumentNullException(nameof(updateAction));
            }

            AppState updatedState = CloneAppState(appState);
            updateAction(updatedState);
            appState = CloneAppState(updatedState);
            return Task.FromResult(CloneAppState(appState));
        }

        public Task<AppState> UpdateAppStateImmediatelyAsync(Action<AppState> updateAction)
        {
            return UpdateAppStateAsync(updateAction);
        }

        public Task SaveProjectStateAsync(string projectId, ProjectState state)
        {
            projectStatesById[projectId] = CloneProjectState(state);
            return Task.CompletedTask;
        }

        public Task<ProjectState> LoadProjectStateAsync(string projectId)
        {
            if (projectStatesById.TryGetValue(projectId, out ProjectState? state))
            {
                return Task.FromResult(CloneProjectState(state));
            }

            return Task.FromResult(new ProjectState
            {
                ActiveStageFolderName = null,
                ZoomSliderValue = 160,
                SelectedAiModelName = null,
                LastInspectedImagePath = null,
            });
        }

        public Task FlushPendingSavesAsync()
        {
            return Task.CompletedTask;
        }

        public void SetAppState(AppState state)
        {
            appState = CloneAppState(state);
        }

        public void SetProjectState(string projectId, ProjectState state)
        {
            projectStatesById[projectId] = CloneProjectState(state);
        }

        private static AppState CloneAppState(AppState state)
        {
            return new AppState
            {
                LastOpenedProjectId = state.LastOpenedProjectId,
                WindowWidth = state.WindowWidth,
                WindowHeight = state.WindowHeight,
                WindowX = state.WindowX,
                WindowY = state.WindowY,
                LastMasterRootDirectory = state.LastMasterRootDirectory,
            };
        }

        private static ProjectState CloneProjectState(ProjectState state)
        {
            return new ProjectState
            {
                ActiveStageFolderName = state.ActiveStageFolderName,
                ZoomSliderValue = state.ZoomSliderValue,
                SelectedAiModelName = state.SelectedAiModelName,
                LastInspectedImagePath = state.LastInspectedImagePath,
            };
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
            ImageTaggingResult taggingResult = new ImageTaggingResult
            {
                AcceptedTrainingTags = new[] { "sample", modelName },
            };
            TagGenerationCompleted?.Invoke(this, new AiTaggingCompletedMessage(imageFilePath, taggingResult));
            return Task.FromResult<IReadOnlyList<string>>(new[] { "sample", modelName });
        }

        public bool TryQueueTagGeneration(Project project, string imageFilePath)
        {
            string? modelName = AiTaggingModelResolver.ResolveConfiguredModelName(project);
            if (string.IsNullOrWhiteSpace(modelName))
            {
                return false;
            }

            ImageTaggingResult taggingResult = new ImageTaggingResult
            {
                AcceptedTrainingTags = new[] { "sample", modelName },
            };
            TagGenerationCompleted?.Invoke(this, new AiTaggingCompletedMessage(imageFilePath, taggingResult));
            return true;
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
                    IsInstalled = true,
                },
                new AiModelInfo
                {
                    Id = "convnext-v2",
                    DisplayName = "ConvNext V2",
                    ModelPath = "models/convnext-v2.onnx",
                    IsInstalled = true,
                },
            };

            return Task.FromResult(models);
        }

        public async Task<AiModelInfo?> DownloadModelAsync(string modelId, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<AiModelInfo> models = await GetAvailableModelsAsync().ConfigureAwait(false);
            return models.FirstOrDefault(model => string.Equals(model.Id, modelId, StringComparison.OrdinalIgnoreCase));
        }

        public bool IsModelDownloadInProgress(string modelId)
        {
            _ = modelId;
            return false;
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
            byte[] thumbnailBytes = CreateSampleImageBytes(imageFilePath, size);
            MemoryStream memoryStream = new MemoryStream(thumbnailBytes, writable: false);
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
