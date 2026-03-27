using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DatasetStudio.Models;
using DatasetStudio.Services;

namespace DatasetStudio.Tests.TestDoubles;

public sealed class InMemoryProjectService : IProjectService
{
    private readonly List<Project> projects;

    public InMemoryProjectService(IEnumerable<Project> projects)
    {
        this.projects = projects?.ToList() ?? throw new ArgumentNullException(nameof(projects));
    }

    public Task<IReadOnlyList<Project>> LoadProjectsAsync()
    {
        return Task.FromResult<IReadOnlyList<Project>>(projects);
    }

    public Task<Project> CreateProjectAsync(string name, string rootFolder)
    {
        Project project = new Project
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name,
            RootFolderPath = rootFolder,
        };

        projects.Add(project);
        return Task.FromResult(project);
    }

    public Task SaveProjectAsync(Project project)
    {
        Project? existingProject = projects.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, project.Id, StringComparison.OrdinalIgnoreCase));

        if (existingProject is null)
        {
            projects.Add(project);
            return Task.CompletedTask;
        }

        int index = projects.IndexOf(existingProject);
        projects[index] = project;
        return Task.CompletedTask;
    }

    public Task DeleteProjectAsync(string projectId)
    {
        Project? existingProject = projects.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, projectId, StringComparison.OrdinalIgnoreCase));

        if (existingProject is not null)
        {
            projects.Remove(existingProject);
        }

        return Task.CompletedTask;
    }
}
