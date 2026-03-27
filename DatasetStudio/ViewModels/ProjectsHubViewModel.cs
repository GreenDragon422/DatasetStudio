using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DatasetStudio.Messages;
using DatasetStudio.Models;
using DatasetStudio.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Specialized;

namespace DatasetStudio.ViewModels;

public partial class ProjectsHubViewModel : ScreenViewModelBase, IDisposable
{
    private const string DefaultNewProjectName = "New Project";

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IFileSystemService fileSystemService;
    private readonly INavigationService navigationService;
    private readonly IMessenger messenger;
    private readonly IProjectService projectService;
    private CancellationTokenSource? masterRootWatcherRefreshCancellationSource;
    private FileSystemWatcher? masterRootWatcher;

    public ProjectsHubViewModel(
        IProjectService projectService,
        IFileSystemService fileSystemService,
        INavigationService navigationService,
        IMessenger messenger)
        : base(messenger)
    {
        this.projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
        this.fileSystemService = fileSystemService ?? throw new ArgumentNullException(nameof(fileSystemService));
        this.navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        this.messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));

        Projects = new ObservableCollection<ProjectsHubProjectCardViewModel>();
        Projects.CollectionChanged += OnProjectsCollectionChanged;
        messenger.Register<ProjectsHubViewModel, ProjectConfigSavedMessage>(this, static (recipient, message) =>
        {
            _ = recipient.RefreshAfterProjectConfigSavedAsync(message.ProjectId);
        });
        StatusText = "Projects Hub ready.";
        IsEmptyStateVisible = true;
    }

    [ObservableProperty]
    private ObservableCollection<ProjectsHubProjectCardViewModel> projects;

    [ObservableProperty]
    private string masterRootPath = string.Empty;

    [ObservableProperty]
    private bool isScanning;

    [ObservableProperty]
    private bool hasProjects;

    [ObservableProperty]
    private bool isEmptyStateVisible;

    [RelayCommand]
    private async Task LoadProjectsAsync()
    {
        await LoadProjectsFromServiceAsync().ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task ScanMasterRootAsync()
    {
        if (string.IsNullOrWhiteSpace(MasterRootPath))
        {
            StatusText = "Select a master root folder before scanning.";
            return;
        }

        if (!Directory.Exists(MasterRootPath))
        {
            StatusText = "The selected master root folder does not exist.";
            return;
        }

        IsScanning = true;
        StatusText = "Scanning master root for projects...";

        try
        {
            IReadOnlyList<string> projectFolders = await fileSystemService.DiscoverProjectFoldersAsync(MasterRootPath).ConfigureAwait(false);
            List<ProjectsHubProjectCardViewModel> cards = new();

            foreach (string projectFolderPath in projectFolders)
            {
                Project project = await LoadProjectFromFolderAsync(projectFolderPath).ConfigureAwait(false);
                ProjectsHubProjectCardViewModel card = await BuildCardAsync(project).ConfigureAwait(false);
                cards.Add(card);
            }

            await ReplaceProjectsAsync(cards).ConfigureAwait(false);
            StatusText = string.Format("Found {0} project{1} in {2}.", cards.Count, cards.Count == 1 ? string.Empty : "s", MasterRootPath);
        }
        catch (Exception exception)
        {
            StatusText = string.Format("Scan failed: {0}", exception.Message);
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    private async Task NewProjectAsync()
    {
        if (string.IsNullOrWhiteSpace(MasterRootPath))
        {
            StatusText = "Choose a master root folder before creating a project.";
            return;
        }

        string newProjectFolderPath = GetUniqueProjectFolderPath(MasterRootPath, DefaultNewProjectName);
        string projectName = Path.GetFileName(newProjectFolderPath);

        try
        {
            Project project = await projectService.CreateProjectAsync(projectName, newProjectFolderPath).ConfigureAwait(false);
            ProjectsHubProjectCardViewModel card = await BuildCardAsync(project).ConfigureAwait(false);
            Projects.Insert(0, card);
            UpdateProjectVisibility();
            StatusText = string.Format("Created {0}. Finish configuring it in the modal.", project.Name);
            messenger.Send(new OpenProjectConfigurationRequestedMessage(project));
        }
        catch (Exception exception)
        {
            StatusText = string.Format("Could not create project: {0}", exception.Message);
        }
    }

    [RelayCommand]
    private void OpenProject(ProjectsHubProjectCardViewModel? projectCard)
    {
        if (projectCard is null)
        {
            return;
        }

        navigationService.NavigateTo<LibraryGridViewModel>(projectCard.Project);
        messenger.Send(new ProjectOpenedMessage(projectCard.ProjectId));
        StatusText = string.Format("Project selected: {0}", projectCard.Name);
    }

    private async Task LoadProjectsFromServiceAsync()
    {
        IsScanning = true;
        StatusText = "Loading projects from saved session...";

        try
        {
            IReadOnlyList<Project> projects = await projectService.LoadProjectsAsync().ConfigureAwait(false);
            List<ProjectsHubProjectCardViewModel> cards = new();

            foreach (Project project in projects)
            {
                ProjectsHubProjectCardViewModel card = await BuildCardAsync(project).ConfigureAwait(false);
                cards.Add(card);
            }

            await ReplaceProjectsAsync(cards).ConfigureAwait(false);

            if (cards.Count > 0)
            {
                string parentFolderPath = Path.GetDirectoryName(cards[0].RootFolderPath) ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(parentFolderPath))
                {
                    MasterRootPath = parentFolderPath;
                }
            }

            StatusText = string.Format("Loaded {0} project{1}.", cards.Count, cards.Count == 1 ? string.Empty : "s");
        }
        catch (Exception exception)
        {
            StatusText = string.Format("Load failed: {0}", exception.Message);
        }
        finally
        {
            IsScanning = false;
        }
    }

    private async Task<ProjectsHubProjectCardViewModel> BuildCardAsync(Project project)
    {
        int imageCount = 0;
        int taggedImageCount = 0;
        IReadOnlyList<string> stageFolderPaths = GetStageFolderPaths(project);

        foreach (string stageFolderPath in stageFolderPaths)
        {
            IReadOnlyList<string> imageFilePaths = await fileSystemService.GetImageFilesAsync(stageFolderPath).ConfigureAwait(false);

            foreach (string imageFilePath in imageFilePaths)
            {
                imageCount++;
                string tagFilePath = Path.ChangeExtension(imageFilePath, ".txt");

                if (File.Exists(tagFilePath) && new FileInfo(tagFilePath).Length > 0)
                {
                    taggedImageCount++;
                }
            }
        }

        return new ProjectsHubProjectCardViewModel(
            project,
            project.Id,
            string.IsNullOrWhiteSpace(project.Name) ? Path.GetFileName(project.RootFolderPath) : project.Name,
            project.RootFolderPath,
            imageCount,
            taggedImageCount,
            OpenProjectCommand);
    }

    private static IReadOnlyList<string> GetStageFolderPaths(Project project)
    {
        if (project.Stages.Count == 0)
        {
            return new List<string> { project.RootFolderPath };
        }

        List<string> stageFolderPaths = new();
        foreach (WorkflowStage stage in project.Stages)
        {
            stageFolderPaths.Add(Path.Combine(project.RootFolderPath, stage.FolderName));
        }

        return stageFolderPaths;
    }

    private static string GetUniqueProjectFolderPath(string masterRootPath, string projectName)
    {
        string safeProjectFolderName = projectName.Trim();
        if (string.IsNullOrWhiteSpace(safeProjectFolderName))
        {
            safeProjectFolderName = DefaultNewProjectName;
        }

        string projectFolderPath = Path.Combine(masterRootPath, safeProjectFolderName);
        int index = 2;

        while (Directory.Exists(projectFolderPath))
        {
            projectFolderPath = Path.Combine(masterRootPath, string.Format("{0} {1}", safeProjectFolderName, index));
            index++;
        }

        return projectFolderPath;
    }

    private async Task<Project> LoadProjectFromFolderAsync(string projectFolderPath)
    {
        string projectConfigurationPath = Path.Combine(projectFolderPath, ".datasetstudio.json");

        if (!File.Exists(projectConfigurationPath))
        {
            return CreateFallbackProject(projectFolderPath);
        }

        try
        {
            await using FileStream fileStream = File.OpenRead(projectConfigurationPath);
            Project? project = await JsonSerializer.DeserializeAsync<Project>(fileStream, JsonSerializerOptions).ConfigureAwait(false);

            if (project is null)
            {
                return CreateFallbackProject(projectFolderPath);
            }

            project.RootFolderPath = projectFolderPath;
            if (string.IsNullOrWhiteSpace(project.Name))
            {
                project.Name = Path.GetFileName(projectFolderPath);
            }

            return project;
        }
        catch (JsonException)
        {
            return CreateFallbackProject(projectFolderPath);
        }
    }

    private static Project CreateFallbackProject(string projectFolderPath)
    {
        return new Project
        {
            Id = Guid.NewGuid().ToString(),
            Name = Path.GetFileName(projectFolderPath),
            RootFolderPath = projectFolderPath,
            AiModelName = string.Empty,
            LastModified = DateTime.UtcNow,
            PrefixTags = new List<string>(),
            Stages = new List<WorkflowStage>(),
            TagDictionaryEntries = new List<TagDictionaryEntry>(),
            State = new ProjectState(),
        };
    }

    private async Task ReplaceProjectsAsync(IReadOnlyList<ProjectsHubProjectCardViewModel> projectCards)
    {
        Projects.Clear();

        foreach (ProjectsHubProjectCardViewModel projectCard in projectCards)
        {
            Projects.Add(projectCard);
        }

        UpdateProjectVisibility();
        await Task.CompletedTask.ConfigureAwait(false);
    }

    private async Task RefreshAfterProjectConfigSavedAsync(string projectId)
    {
        await LoadProjectsFromServiceAsync().ConfigureAwait(false);
        StatusText = string.Format("Project configuration saved for {0}.", projectId);
    }

    private void OnProjectsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs eventArgs)
    {
        UpdateProjectVisibility();
    }

    private void UpdateProjectVisibility()
    {
        HasProjects = Projects.Count > 0;
        IsEmptyStateVisible = !HasProjects;
    }

    partial void OnMasterRootPathChanged(string value)
    {
        ConfigureMasterRootWatcher(value);
    }

    public void Dispose()
    {
        Projects.CollectionChanged -= OnProjectsCollectionChanged;
        masterRootWatcherRefreshCancellationSource?.Cancel();
        masterRootWatcherRefreshCancellationSource?.Dispose();
        DetachMasterRootWatcher();
    }

    private void ConfigureMasterRootWatcher(string masterRootFolderPath)
    {
        DetachMasterRootWatcher();

        if (string.IsNullOrWhiteSpace(masterRootFolderPath) || !Directory.Exists(masterRootFolderPath))
        {
            return;
        }

        FileSystemWatcher fileSystemWatcher = fileSystemService.WatchFolder(masterRootFolderPath);
        fileSystemWatcher.Changed += OnMasterRootWatcherChanged;
        fileSystemWatcher.Created += OnMasterRootWatcherChanged;
        fileSystemWatcher.Deleted += OnMasterRootWatcherChanged;
        fileSystemWatcher.Renamed += OnMasterRootWatcherRenamed;
        fileSystemWatcher.EnableRaisingEvents = true;
        masterRootWatcher = fileSystemWatcher;
    }

    private void DetachMasterRootWatcher()
    {
        if (masterRootWatcher is null)
        {
            return;
        }

        masterRootWatcher.Changed -= OnMasterRootWatcherChanged;
        masterRootWatcher.Created -= OnMasterRootWatcherChanged;
        masterRootWatcher.Deleted -= OnMasterRootWatcherChanged;
        masterRootWatcher.Renamed -= OnMasterRootWatcherRenamed;
        masterRootWatcher.Dispose();
        masterRootWatcher = null;
    }

    private void OnMasterRootWatcherChanged(object? sender, FileSystemEventArgs eventArgs)
    {
        _ = sender;

        if (!ShouldReactToMasterRootChange(eventArgs.FullPath))
        {
            return;
        }

        QueueMasterRootRescan();
    }

    private void OnMasterRootWatcherRenamed(object? sender, RenamedEventArgs eventArgs)
    {
        _ = sender;

        if (!ShouldReactToMasterRootChange(eventArgs.OldFullPath)
            && !ShouldReactToMasterRootChange(eventArgs.FullPath))
        {
            return;
        }

        QueueMasterRootRescan();
    }

    private bool ShouldReactToMasterRootChange(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath) || string.IsNullOrWhiteSpace(MasterRootPath))
        {
            return false;
        }

        string relativePath = Path.GetRelativePath(MasterRootPath, fullPath);
        if (relativePath.StartsWith("..", StringComparison.Ordinal))
        {
            return false;
        }

        string[] pathSegments = relativePath
            .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);

        if (pathSegments.Length == 1)
        {
            return true;
        }

        return pathSegments.Length == 2
            && string.Equals(pathSegments[1], ".datasetstudio.json", StringComparison.OrdinalIgnoreCase);
    }

    private void QueueMasterRootRescan()
    {
        if (string.IsNullOrWhiteSpace(MasterRootPath) || !Directory.Exists(MasterRootPath))
        {
            return;
        }

        masterRootWatcherRefreshCancellationSource?.Cancel();
        masterRootWatcherRefreshCancellationSource?.Dispose();

        CancellationTokenSource cancellationTokenSource = new();
        masterRootWatcherRefreshCancellationSource = cancellationTokenSource;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(350, cancellationTokenSource.Token).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            Dispatcher.UIThread.Post(() =>
            {
                if (!IsScanning)
                {
                    _ = ScanMasterRootAsync();
                }
            });
        });
    }
}
