using System.Threading.Tasks;
using DatasetStudio.Models;

namespace DatasetStudio.Services;

public interface IStatePersistenceService
{
    Task SaveAppStateAsync(AppState state);

    Task<AppState> LoadAppStateAsync();

    Task SaveProjectStateAsync(string projectId, ProjectState state);

    Task<ProjectState> LoadProjectStateAsync(string projectId);
}
