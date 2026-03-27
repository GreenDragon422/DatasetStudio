using DatasetStudio.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace DatasetStudio.Services;

public sealed class ProjectService : IProjectService
{
    private const string ProjectConfigurationFileName = ".datasetstudio.json";
    private const int DefaultZoomSliderValue = 160;

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly IFileSystemService fileSystemService;
    private readonly IStatePersistenceService statePersistenceService;
    private readonly Dictionary<string, string> projectRootPathsById;

    public ProjectService(IFileSystemService fileSystemService, IStatePersistenceService statePersistenceService)
    {
        this.fileSystemService = fileSystemService ?? throw new ArgumentNullException(nameof(fileSystemService));
        this.statePersistenceService = statePersistenceService ?? throw new ArgumentNullException(nameof(statePersistenceService));
        projectRootPathsById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyList<Project>> LoadProjectsAsync()
    {
        AppState appState = await statePersistenceService.LoadAppStateAsync();

        if (string.IsNullOrWhiteSpace(appState.LastMasterRootDirectory))
        {
            return [];
        }

        IReadOnlyList<string> projectFolders = await fileSystemService.DiscoverProjectFoldersAsync(appState.LastMasterRootDirectory);
        List<Project> projects = new();

        foreach (string projectFolder in projectFolders)
        {
            Project project = await LoadProjectFromFolderAsync(projectFolder);
            projects.Add(project);
            projectRootPathsById[project.Id] = project.RootFolderPath;
        }

        return projects
            .OrderBy(project => project.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(project => project.RootFolderPath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<Project> CreateProjectAsync(string name, string rootFolder)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Project name is required.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(rootFolder))
        {
            throw new ArgumentException("Project root folder is required.", nameof(rootFolder));
        }

        await fileSystemService.EnsureFolderExistsAsync(rootFolder);

        IReadOnlyList<WorkflowStage> stages = DiscoverWorkflowStages(rootFolder);
        Project project = CreateDefaultProject(name, rootFolder, stages);

        await SaveProjectAsync(project);
        return project;
    }

    public async Task SaveProjectAsync(Project project)
    {
        if (project is null)
        {
            throw new ArgumentNullException(nameof(project));
        }

        if (string.IsNullOrWhiteSpace(project.RootFolderPath))
        {
            throw new ArgumentException("Project root folder path is required.", nameof(project));
        }

        if (string.IsNullOrWhiteSpace(project.Id))
        {
            project.Id = Guid.NewGuid().ToString();
        }

        project.State ??= CreateDefaultProjectState(project.Stages, project.AiModelName);
        project.LastModified = DateTime.UtcNow;

        await fileSystemService.EnsureFolderExistsAsync(project.RootFolderPath);

        string projectConfigurationPath = GetProjectConfigurationPath(project.RootFolderPath);
        await using FileStream fileStream = File.Create(projectConfigurationPath);
        await JsonSerializer.SerializeAsync(fileStream, project, JsonSerializerOptions);

        projectRootPathsById[project.Id] = project.RootFolderPath;
    }

    public async Task DeleteProjectAsync(string projectId)
    {
        if (string.IsNullOrWhiteSpace(projectId))
        {
            throw new ArgumentException("Project id is required.", nameof(projectId));
        }

        string? projectRootPath = await ResolveProjectRootPathAsync(projectId);

        if (string.IsNullOrWhiteSpace(projectRootPath))
        {
            return;
        }

        string projectConfigurationPath = GetProjectConfigurationPath(projectRootPath);

        if (File.Exists(projectConfigurationPath))
        {
            File.Delete(projectConfigurationPath);
        }

        projectRootPathsById.Remove(projectId);
    }

    private async Task<Project> LoadProjectFromFolderAsync(string rootFolderPath)
    {
        string projectConfigurationPath = GetProjectConfigurationPath(rootFolderPath);

        if (!File.Exists(projectConfigurationPath))
        {
            Project defaultProject = CreateDefaultProject(Path.GetFileName(rootFolderPath), rootFolderPath, DiscoverWorkflowStages(rootFolderPath));
            await SaveProjectAsync(defaultProject);
            return defaultProject;
        }

        try
        {
            await using FileStream fileStream = File.OpenRead(projectConfigurationPath);
            Project? project = await JsonSerializer.DeserializeAsync<Project>(fileStream, JsonSerializerOptions);

            if (project is null)
            {
                return CreateDefaultProject(Path.GetFileName(rootFolderPath), rootFolderPath, DiscoverWorkflowStages(rootFolderPath));
            }

            project.RootFolderPath = rootFolderPath;
            project.State ??= CreateDefaultProjectState(project.Stages, project.AiModelName);

            if (project.Stages.Count == 0)
            {
                project.Stages = DiscoverWorkflowStages(rootFolderPath).ToList();
            }

            if (string.IsNullOrWhiteSpace(project.Name))
            {
                project.Name = Path.GetFileName(rootFolderPath);
            }

            if (string.IsNullOrWhiteSpace(project.Id))
            {
                project.Id = Guid.NewGuid().ToString();
            }

            project.LastModified = File.GetLastWriteTimeUtc(projectConfigurationPath);
            return project;
        }
        catch (JsonException)
        {
            return CreateDefaultProject(Path.GetFileName(rootFolderPath), rootFolderPath, DiscoverWorkflowStages(rootFolderPath));
        }
    }

    private static IReadOnlyList<WorkflowStage> DiscoverWorkflowStages(string rootFolderPath)
    {
        if (!Directory.Exists(rootFolderPath))
        {
            return [];
        }

        List<string> folderNames = new();

        foreach (string directoryPath in Directory.EnumerateDirectories(rootFolderPath))
        {
            string? folderName = Path.GetFileName(directoryPath);

            if (!string.IsNullOrWhiteSpace(folderName))
            {
                folderNames.Add(folderName);
            }
        }

        return WorkflowStageParser.ParseAndSort(folderNames);
    }

    private static Project CreateDefaultProject(string name, string rootFolderPath, IReadOnlyList<WorkflowStage> stages)
    {
        return new Project
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            RootFolderPath = rootFolderPath,
            Stages = stages.ToList(),
            PrefixTags = new List<string>(),
            AiModelName = string.Empty,
            LastModified = DateTime.UtcNow,
            TagDictionaryEntries = new List<TagDictionaryEntry>(),
            State = CreateDefaultProjectState(stages, string.Empty),
        };
    }

    private static ProjectState CreateDefaultProjectState(IReadOnlyList<WorkflowStage> stages, string? selectedAiModelName)
    {
        return new ProjectState
        {
            ActiveStageFolderName = stages.FirstOrDefault()?.FolderName,
            ZoomSliderValue = DefaultZoomSliderValue,
            SelectedAiModelName = string.IsNullOrWhiteSpace(selectedAiModelName) ? null : selectedAiModelName,
            LastInspectedImagePath = null,
        };
    }

    private static string GetProjectConfigurationPath(string rootFolderPath)
    {
        return Path.Combine(rootFolderPath, ProjectConfigurationFileName);
    }

    private async Task<string?> ResolveProjectRootPathAsync(string projectId)
    {
        if (projectRootPathsById.TryGetValue(projectId, out string? knownProjectRootPath))
        {
            return knownProjectRootPath;
        }

        AppState appState = await statePersistenceService.LoadAppStateAsync();

        if (string.IsNullOrWhiteSpace(appState.LastMasterRootDirectory))
        {
            return null;
        }

        IReadOnlyList<string> projectFolders = await fileSystemService.DiscoverProjectFoldersAsync(appState.LastMasterRootDirectory);

        foreach (string projectFolder in projectFolders)
        {
            string projectConfigurationPath = GetProjectConfigurationPath(projectFolder);

            try
            {
                await using FileStream fileStream = File.OpenRead(projectConfigurationPath);
                Project? project = await JsonSerializer.DeserializeAsync<Project>(fileStream, JsonSerializerOptions);

                if (project?.Id == projectId)
                {
                    projectRootPathsById[projectId] = projectFolder;
                    return projectFolder;
                }
            }
            catch (JsonException)
            {
            }
        }

        return null;
    }
}
