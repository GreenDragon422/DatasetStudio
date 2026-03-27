using System.Collections.Generic;
using System.Threading.Tasks;
using DatasetStudio.Models;

namespace DatasetStudio.Services;

public interface IProjectService
{
    Task<IReadOnlyList<Project>> LoadProjectsAsync();

    Task<Project> CreateProjectAsync(string name, string rootFolder);

    Task SaveProjectAsync(Project project);

    Task DeleteProjectAsync(string projectId);
}
