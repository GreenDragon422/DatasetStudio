using DatasetStudio.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DatasetStudio.Services;

public sealed class StatePersistenceService : IStatePersistenceService
{
    private const string ApplicationStateFileName = "datasetstudio-settings.json";
    private const string ProjectConfigurationFileName = ".datasetstudio.json";
    private const int DefaultZoomSliderValue = 160;
    private static readonly TimeSpan DefaultDebounceDelay = TimeSpan.FromMilliseconds(500);

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly IFileSystemService fileSystemService;
    private readonly string applicationStateFilePath;
    private readonly TimeSpan debounceDelay;
    private readonly object syncLock;
    private readonly Dictionary<string, string> projectConfigurationPathsById;
    private readonly Dictionary<string, ProjectState> pendingProjectStatesById;
    private readonly Dictionary<string, Timer> projectStateSaveTimersById;
    private readonly Dictionary<string, List<TaskCompletionSource<bool>>> pendingProjectStateSaveCompletionsById;
    private readonly List<TaskCompletionSource<bool>> pendingApplicationStateSaveCompletions;
    private readonly SemaphoreSlim applicationStateUpdateSemaphore;

    private Timer? applicationStateSaveTimer;
    private AppState? pendingApplicationState;

    public StatePersistenceService(IFileSystemService fileSystemService, string? applicationDataDirectoryPath = null, TimeSpan? debounceDelay = null)
    {
        this.fileSystemService = fileSystemService ?? throw new ArgumentNullException(nameof(fileSystemService));
        string resolvedApplicationDataDirectoryPath = string.IsNullOrWhiteSpace(applicationDataDirectoryPath)
            ? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
            : applicationDataDirectoryPath;

        applicationStateFilePath = Path.Combine(resolvedApplicationDataDirectoryPath, ApplicationStateFileName);
        this.debounceDelay = debounceDelay ?? DefaultDebounceDelay;
        syncLock = new object();
        projectConfigurationPathsById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        pendingProjectStatesById = new Dictionary<string, ProjectState>(StringComparer.OrdinalIgnoreCase);
        projectStateSaveTimersById = new Dictionary<string, Timer>(StringComparer.OrdinalIgnoreCase);
        pendingProjectStateSaveCompletionsById = new Dictionary<string, List<TaskCompletionSource<bool>>>(StringComparer.OrdinalIgnoreCase);
        pendingApplicationStateSaveCompletions = new List<TaskCompletionSource<bool>>();
        applicationStateUpdateSemaphore = new SemaphoreSlim(1, 1);
    }

    public Task SaveAppStateAsync(AppState state)
    {
        if (state is null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        TaskCompletionSource<bool> completionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
        AppState clonedState = CloneAppState(state);

        lock (syncLock)
        {
            pendingApplicationState = clonedState;
            pendingApplicationStateSaveCompletions.Add(completionSource);

            if (applicationStateSaveTimer is null)
            {
                applicationStateSaveTimer = new Timer(OnApplicationStateSaveTimerElapsed, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            }

            applicationStateSaveTimer.Change(debounceDelay, Timeout.InfiniteTimeSpan);
        }

        return completionSource.Task;
    }

    public async Task<AppState> LoadAppStateAsync()
    {
        lock (syncLock)
        {
            if (pendingApplicationState is not null)
            {
                return CloneAppState(pendingApplicationState);
            }
        }

        if (!File.Exists(applicationStateFilePath))
        {
            return new AppState();
        }

        try
        {
            await using FileStream fileStream = File.OpenRead(applicationStateFilePath);
            AppState? appState = await JsonSerializer.DeserializeAsync<AppState>(fileStream, JsonSerializerOptions);
            return appState ?? new AppState();
        }
        catch (JsonException)
        {
            return new AppState();
        }
    }

    public async Task<AppState> UpdateAppStateAsync(Action<AppState> updateAction)
    {
        if (updateAction is null)
        {
            throw new ArgumentNullException(nameof(updateAction));
        }

        await applicationStateUpdateSemaphore.WaitAsync().ConfigureAwait(false);

        try
        {
            AppState appState = await LoadAppStateAsync().ConfigureAwait(false);
            updateAction(appState);
            await SaveAppStateAsync(appState).ConfigureAwait(false);
            return CloneAppState(appState);
        }
        finally
        {
            applicationStateUpdateSemaphore.Release();
        }
    }

    public async Task SaveProjectStateAsync(string projectId, ProjectState state)
    {
        if (string.IsNullOrWhiteSpace(projectId))
        {
            throw new ArgumentException("Project id is required.", nameof(projectId));
        }

        if (state is null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        string? projectConfigurationPath = await ResolveProjectConfigurationPathAsync(projectId);

        if (string.IsNullOrWhiteSpace(projectConfigurationPath))
        {
            return;
        }

        TaskCompletionSource<bool> completionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ProjectState clonedState = CloneProjectState(state);

        lock (syncLock)
        {
            projectConfigurationPathsById[projectId] = projectConfigurationPath;
            pendingProjectStatesById[projectId] = clonedState;

            if (!pendingProjectStateSaveCompletionsById.TryGetValue(projectId, out List<TaskCompletionSource<bool>>? completionSources))
            {
                completionSources = new List<TaskCompletionSource<bool>>();
                pendingProjectStateSaveCompletionsById[projectId] = completionSources;
            }

            completionSources.Add(completionSource);

            if (!projectStateSaveTimersById.TryGetValue(projectId, out Timer? projectStateSaveTimer))
            {
                projectStateSaveTimer = new Timer(OnProjectStateSaveTimerElapsed, projectId, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                projectStateSaveTimersById[projectId] = projectStateSaveTimer;
            }

            projectStateSaveTimer.Change(debounceDelay, Timeout.InfiniteTimeSpan);
        }

        await completionSource.Task;
    }

    public async Task<ProjectState> LoadProjectStateAsync(string projectId)
    {
        if (string.IsNullOrWhiteSpace(projectId))
        {
            throw new ArgumentException("Project id is required.", nameof(projectId));
        }

        lock (syncLock)
        {
            if (pendingProjectStatesById.TryGetValue(projectId, out ProjectState? pendingProjectState))
            {
                return CloneProjectState(pendingProjectState);
            }
        }

        string? projectConfigurationPath = await ResolveProjectConfigurationPathAsync(projectId);

        if (string.IsNullOrWhiteSpace(projectConfigurationPath) || !File.Exists(projectConfigurationPath))
        {
            return CreateDefaultProjectState();
        }

        try
        {
            await using FileStream fileStream = File.OpenRead(projectConfigurationPath);
            Project? project = await JsonSerializer.DeserializeAsync<Project>(fileStream, JsonSerializerOptions);

            if (project?.State is null)
            {
                return CreateDefaultProjectState();
            }

            return NormalizeProjectState(project.State);
        }
        catch (JsonException)
        {
            return CreateDefaultProjectState();
        }
    }

    public Task FlushPendingSavesAsync()
    {
        AppState? applicationStateToPersist;
        List<TaskCompletionSource<bool>> applicationCompletionSources;
        Dictionary<string, ProjectState> projectStatesToPersist;
        Dictionary<string, string> projectConfigurationPathsToPersist;
        Dictionary<string, List<TaskCompletionSource<bool>>> projectCompletionSourcesById;

        lock (syncLock)
        {
            if (applicationStateSaveTimer is not null)
            {
                applicationStateSaveTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            }

            foreach (Timer projectStateSaveTimer in projectStateSaveTimersById.Values)
            {
                projectStateSaveTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            }

            applicationStateToPersist = pendingApplicationState;
            pendingApplicationState = null;
            applicationCompletionSources = new List<TaskCompletionSource<bool>>(pendingApplicationStateSaveCompletions);
            pendingApplicationStateSaveCompletions.Clear();

            projectStatesToPersist = new Dictionary<string, ProjectState>(pendingProjectStatesById, StringComparer.OrdinalIgnoreCase);
            pendingProjectStatesById.Clear();
            projectConfigurationPathsToPersist = new Dictionary<string, string>(projectConfigurationPathsById, StringComparer.OrdinalIgnoreCase);
            projectCompletionSourcesById = new Dictionary<string, List<TaskCompletionSource<bool>>>(StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, List<TaskCompletionSource<bool>>> entry in pendingProjectStateSaveCompletionsById)
            {
                projectCompletionSourcesById[entry.Key] = new List<TaskCompletionSource<bool>>(entry.Value);
            }

            pendingProjectStateSaveCompletionsById.Clear();
        }

        List<Exception> exceptions = new List<Exception>();

        try
        {
            if (applicationStateToPersist is not null)
            {
                PersistApplicationState(applicationStateToPersist);
            }

            CompleteSuccessfully(applicationCompletionSources);
        }
        catch (Exception exception)
        {
            exceptions.Add(exception);
            CompleteWithException(applicationCompletionSources, exception);
        }

        foreach (KeyValuePair<string, ProjectState> entry in projectStatesToPersist)
        {
            string projectId = entry.Key;
            ProjectState projectStateToPersist = entry.Value;
            List<TaskCompletionSource<bool>> projectCompletionSources = projectCompletionSourcesById.TryGetValue(projectId, out List<TaskCompletionSource<bool>>? storedCompletionSources)
                ? storedCompletionSources
                : new List<TaskCompletionSource<bool>>();

            try
            {
                if (projectConfigurationPathsToPersist.TryGetValue(projectId, out string? projectConfigurationPath)
                    && !string.IsNullOrWhiteSpace(projectConfigurationPath))
                {
                    PersistProjectState(projectId, projectConfigurationPath, projectStateToPersist);
                }

                CompleteSuccessfully(projectCompletionSources);
            }
            catch (Exception exception)
            {
                exceptions.Add(exception);
                CompleteWithException(projectCompletionSources, exception);
            }
        }

        if (exceptions.Count > 0)
        {
            return Task.FromException(new AggregateException(exceptions));
        }

        return Task.CompletedTask;
    }

    private void OnApplicationStateSaveTimerElapsed(object? timerState)
    {
        AppState? applicationStateToPersist;
        List<TaskCompletionSource<bool>> completionSources;

        lock (syncLock)
        {
            applicationStateToPersist = pendingApplicationState;
            pendingApplicationState = null;
            completionSources = new List<TaskCompletionSource<bool>>(pendingApplicationStateSaveCompletions);
            pendingApplicationStateSaveCompletions.Clear();
        }

        try
        {
            if (applicationStateToPersist is not null)
            {
                PersistApplicationState(applicationStateToPersist);
            }

            CompleteSuccessfully(completionSources);
        }
        catch (Exception exception)
        {
            CompleteWithException(completionSources, exception);
        }
    }

    private void OnProjectStateSaveTimerElapsed(object? timerState)
    {
        if (timerState is not string projectId)
        {
            return;
        }

        ProjectState? projectStateToPersist;
        string? projectConfigurationPath;
        List<TaskCompletionSource<bool>> completionSources;

        lock (syncLock)
        {
            pendingProjectStatesById.TryGetValue(projectId, out projectStateToPersist);
            pendingProjectStatesById.Remove(projectId);
            projectConfigurationPathsById.TryGetValue(projectId, out projectConfigurationPath);

            if (pendingProjectStateSaveCompletionsById.TryGetValue(projectId, out List<TaskCompletionSource<bool>>? storedCompletionSources))
            {
                completionSources = storedCompletionSources;
                pendingProjectStateSaveCompletionsById.Remove(projectId);
            }
            else
            {
                completionSources = new List<TaskCompletionSource<bool>>();
            }
        }

        try
        {
            if (projectStateToPersist is not null && !string.IsNullOrWhiteSpace(projectConfigurationPath))
            {
                PersistProjectState(projectId, projectConfigurationPath, projectStateToPersist);
            }

            CompleteSuccessfully(completionSources);
        }
        catch (Exception exception)
        {
            CompleteWithException(completionSources, exception);
        }
    }

    private void PersistApplicationState(AppState state)
    {
        string? directoryPath = Path.GetDirectoryName(applicationStateFilePath);

        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        string serializedState = JsonSerializer.Serialize(state, JsonSerializerOptions);
        File.WriteAllText(applicationStateFilePath, serializedState);
    }

    private void PersistProjectState(string projectId, string projectConfigurationPath, ProjectState state)
    {
        Project project = LoadProjectConfiguration(projectId, projectConfigurationPath);
        project.State = NormalizeProjectState(state);
        project.LastModified = DateTime.UtcNow;

        string serializedProject = JsonSerializer.Serialize(project, JsonSerializerOptions);
        File.WriteAllText(projectConfigurationPath, serializedProject);
    }

    private Project LoadProjectConfiguration(string projectId, string projectConfigurationPath)
    {
        string? projectRootPath = Path.GetDirectoryName(projectConfigurationPath);
        string projectName = string.IsNullOrWhiteSpace(projectRootPath) ? projectId : Path.GetFileName(projectRootPath);

        if (string.IsNullOrWhiteSpace(projectRootPath))
        {
            projectRootPath = string.Empty;
        }

        if (!File.Exists(projectConfigurationPath))
        {
            return CreateDefaultProject(projectId, projectName, projectRootPath);
        }

        try
        {
            string serializedProject = File.ReadAllText(projectConfigurationPath);
            Project? project = JsonSerializer.Deserialize<Project>(serializedProject, JsonSerializerOptions);

            if (project is null)
            {
                return CreateDefaultProject(projectId, projectName, projectRootPath);
            }

            project.Id = string.IsNullOrWhiteSpace(project.Id) ? projectId : project.Id;
            project.Name = string.IsNullOrWhiteSpace(project.Name) ? projectName : project.Name;
            project.RootFolderPath = projectRootPath;
            project.State = NormalizeProjectState(project.State);

            return project;
        }
        catch (JsonException)
        {
            return CreateDefaultProject(projectId, projectName, projectRootPath);
        }
    }

    private async Task<string?> ResolveProjectConfigurationPathAsync(string projectId)
    {
        if (projectConfigurationPathsById.TryGetValue(projectId, out string? cachedProjectConfigurationPath) && File.Exists(cachedProjectConfigurationPath))
        {
            return cachedProjectConfigurationPath;
        }

        AppState appState = await LoadAppStateAsync();

        if (string.IsNullOrWhiteSpace(appState.LastMasterRootDirectory))
        {
            return null;
        }

        IReadOnlyList<string> projectFolders = await fileSystemService.DiscoverProjectFoldersAsync(appState.LastMasterRootDirectory);

        foreach (string projectFolder in projectFolders)
        {
            string projectConfigurationPath = Path.Combine(projectFolder, ProjectConfigurationFileName);

            try
            {
                await using FileStream fileStream = File.OpenRead(projectConfigurationPath);
                Project? project = await JsonSerializer.DeserializeAsync<Project>(fileStream, JsonSerializerOptions);

                if (project?.Id == projectId)
                {
                    projectConfigurationPathsById[projectId] = projectConfigurationPath;
                    return projectConfigurationPath;
                }
            }
            catch (JsonException)
            {
            }
        }

        return null;
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

    private static ProjectState NormalizeProjectState(ProjectState? state)
    {
        if (state is null)
        {
            return CreateDefaultProjectState();
        }

        return new ProjectState
        {
            ActiveStageFolderName = state.ActiveStageFolderName,
            ZoomSliderValue = state.ZoomSliderValue <= 0 ? DefaultZoomSliderValue : state.ZoomSliderValue,
            SelectedAiModelName = state.SelectedAiModelName,
            LastInspectedImagePath = state.LastInspectedImagePath,
        };
    }

    private static Project CreateDefaultProject(string projectId, string projectName, string projectRootPath)
    {
        return new Project
        {
            Id = projectId,
            Name = projectName,
            RootFolderPath = projectRootPath,
            Stages = new List<WorkflowStage>(),
            PrefixTags = new List<string>(),
            AiModelName = string.Empty,
            LastModified = DateTime.UtcNow,
            State = CreateDefaultProjectState(),
        };
    }

    private static ProjectState CreateDefaultProjectState()
    {
        return new ProjectState
        {
            ActiveStageFolderName = null,
            ZoomSliderValue = DefaultZoomSliderValue,
            SelectedAiModelName = null,
            LastInspectedImagePath = null,
        };
    }

    private static void CompleteSuccessfully(IEnumerable<TaskCompletionSource<bool>> completionSources)
    {
        foreach (TaskCompletionSource<bool> completionSource in completionSources)
        {
            completionSource.TrySetResult(true);
        }
    }

    private static void CompleteWithException(IEnumerable<TaskCompletionSource<bool>> completionSources, Exception exception)
    {
        foreach (TaskCompletionSource<bool> completionSource in completionSources)
        {
            completionSource.TrySetException(exception);
        }
    }
}
