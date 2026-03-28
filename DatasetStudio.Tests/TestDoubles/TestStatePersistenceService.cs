using DatasetStudio.Models;
using DatasetStudio.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DatasetStudio.Tests.TestDoubles;

public sealed class TestStatePersistenceService : IStatePersistenceService
{
    private readonly Dictionary<string, ProjectState> projectStatesById;
    private AppState appState;

    public TestStatePersistenceService()
    {
        projectStatesById = new Dictionary<string, ProjectState>(StringComparer.OrdinalIgnoreCase);
        appState = new AppState();
    }

    public int AppSaveCount { get; private set; }

    public int ProjectSaveCount { get; private set; }

    public Task SaveAppStateAsync(AppState state)
    {
        appState = CloneAppState(state);
        AppSaveCount++;
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
        AppSaveCount++;
        return Task.FromResult(CloneAppState(appState));
    }

    public Task<AppState> UpdateAppStateImmediatelyAsync(Action<AppState> updateAction)
    {
        return UpdateAppStateAsync(updateAction);
    }

    public Task SaveProjectStateAsync(string projectId, ProjectState state)
    {
        projectStatesById[projectId] = CloneProjectState(state);
        ProjectSaveCount++;
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

    public AppState GetAppState()
    {
        return CloneAppState(appState);
    }

    public ProjectState GetProjectState(string projectId)
    {
        if (!projectStatesById.TryGetValue(projectId, out ProjectState? state))
        {
            return new ProjectState
            {
                ActiveStageFolderName = null,
                ZoomSliderValue = 160,
                SelectedAiModelName = null,
                LastInspectedImagePath = null,
            };
        }

        return CloneProjectState(state);
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
