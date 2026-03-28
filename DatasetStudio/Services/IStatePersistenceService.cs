using System;
using System.Threading.Tasks;
using DatasetStudio.Models;

namespace DatasetStudio.Services;

public interface IStatePersistenceService
{
    Task SaveAppStateAsync(AppState state);

    Task<AppState> LoadAppStateAsync();

    Task<AppState> UpdateAppStateAsync(Action<AppState> updateAction);

    Task<AppState> UpdateAppStateImmediatelyAsync(Action<AppState> updateAction);

    Task SaveProjectStateAsync(string projectId, ProjectState state);

    Task<ProjectState> LoadProjectStateAsync(string projectId);

    Task FlushPendingSavesAsync();
}
