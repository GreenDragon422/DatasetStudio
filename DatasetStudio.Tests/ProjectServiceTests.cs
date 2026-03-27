using DatasetStudio.Models;
using DatasetStudio.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DatasetStudio.Tests;

[TestFixture]
public class ProjectServiceTests
{
    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            DirectoryPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(DirectoryPath);
        }

        public string DirectoryPath { get; }

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, true);
            }
        }
    }

    private sealed class FakeStatePersistenceService : IStatePersistenceService
    {
        public AppState AppState { get; set; } = new AppState();

        public Task<AppState> LoadAppStateAsync()
        {
            return Task.FromResult(AppState);
        }

        public Task<ProjectState> LoadProjectStateAsync(string projectId)
        {
            return Task.FromResult(new ProjectState());
        }

        public Task SaveAppStateAsync(AppState state)
        {
            AppState = state;
            return Task.CompletedTask;
        }

        public Task SaveProjectStateAsync(string projectId, ProjectState state)
        {
            return Task.CompletedTask;
        }

        public Task FlushPendingSavesAsync()
        {
            return Task.CompletedTask;
        }
    }

    [Test]
    public async Task SaveAndLoadProjectsAsync_PreservesProjectConfigurationFields()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string projectRootPath = Path.Combine(temporaryDirectory.DirectoryPath, "Cyberpunk_Cityscapes");
        Directory.CreateDirectory(projectRootPath);
        Directory.CreateDirectory(Path.Combine(projectRootPath, "01_Inbox"));
        Directory.CreateDirectory(Path.Combine(projectRootPath, "02_Review"));

        FakeStatePersistenceService statePersistenceService = new()
        {
            AppState = new AppState
            {
                LastMasterRootDirectory = temporaryDirectory.DirectoryPath,
            },
        };

        FileSystemService fileSystemService = new();
        ProjectService projectService = new(fileSystemService, statePersistenceService);
        Project expectedProject = new()
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Cyberpunk_Cityscapes",
            RootFolderPath = projectRootPath,
            Stages = new List<WorkflowStage>
            {
                new WorkflowStage { Order = 1, FolderName = "01_Inbox", DisplayName = "Inbox" },
                new WorkflowStage { Order = 2, FolderName = "02_Review", DisplayName = "Review" },
            },
            PrefixTags = new List<string> { "lora_style", "masterpiece", "best quality" },
            AiModelName = "wd14-vit-v2",
            State = new ProjectState
            {
                ActiveStageFolderName = "01_Inbox",
                ZoomSliderValue = 220,
                SelectedAiModelName = "wd14-vit-v2",
                LastInspectedImagePath = Path.Combine(projectRootPath, "01_Inbox", "image.png"),
            },
        };

        await projectService.SaveProjectAsync(expectedProject);

        IReadOnlyList<Project> projects = await projectService.LoadProjectsAsync();

        Assert.That(projects, Has.Count.EqualTo(1));

        Project actualProject = projects.Single();

        Assert.That(actualProject.Id, Is.EqualTo(expectedProject.Id));
        Assert.That(actualProject.Name, Is.EqualTo(expectedProject.Name));
        Assert.That(actualProject.RootFolderPath, Is.EqualTo(projectRootPath));
        Assert.That(actualProject.Stages.Select(stage => stage.FolderName), Is.EqualTo(expectedProject.Stages.Select(stage => stage.FolderName)));
        Assert.That(actualProject.PrefixTags, Is.EqualTo(expectedProject.PrefixTags));
        Assert.That(actualProject.AiModelName, Is.EqualTo(expectedProject.AiModelName));
        Assert.That(actualProject.State.ActiveStageFolderName, Is.EqualTo(expectedProject.State.ActiveStageFolderName));
        Assert.That(actualProject.State.ZoomSliderValue, Is.EqualTo(expectedProject.State.ZoomSliderValue));
        Assert.That(actualProject.State.SelectedAiModelName, Is.EqualTo(expectedProject.State.SelectedAiModelName));
        Assert.That(actualProject.State.LastInspectedImagePath, Is.EqualTo(expectedProject.State.LastInspectedImagePath));
    }

    [Test]
    public async Task LoadProjectsAsync_WhenJsonIsMalformed_ReturnsDefaultProjectForFolder()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string projectRootPath = Path.Combine(temporaryDirectory.DirectoryPath, "FallbackProject");
        Directory.CreateDirectory(projectRootPath);
        Directory.CreateDirectory(Path.Combine(projectRootPath, "01_Inbox"));
        Directory.CreateDirectory(Path.Combine(projectRootPath, "02_Review"));
        await File.WriteAllTextAsync(Path.Combine(projectRootPath, ".datasetstudio.json"), "{ invalid json");

        FakeStatePersistenceService statePersistenceService = new()
        {
            AppState = new AppState
            {
                LastMasterRootDirectory = temporaryDirectory.DirectoryPath,
            },
        };

        FileSystemService fileSystemService = new();
        ProjectService projectService = new(fileSystemService, statePersistenceService);

        IReadOnlyList<Project> projects = await projectService.LoadProjectsAsync();

        Assert.That(projects, Has.Count.EqualTo(1));

        Project project = projects.Single();

        Assert.That(project.Name, Is.EqualTo("FallbackProject"));
        Assert.That(project.RootFolderPath, Is.EqualTo(projectRootPath));
        Assert.That(project.Stages.Select(stage => stage.FolderName), Is.EqualTo(new[] { "01_Inbox", "02_Review" }));
        Assert.That(Guid.TryParse(project.Id, out _), Is.True);
        Assert.That(project.State.ActiveStageFolderName, Is.EqualTo("01_Inbox"));
        Assert.That(project.State.ZoomSliderValue, Is.EqualTo(160));
    }

    [Test]
    public async Task CreateProjectAsync_GeneratesGuidAndWritesConfigurationFile()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string projectRootPath = Path.Combine(temporaryDirectory.DirectoryPath, "NewProject");
        Directory.CreateDirectory(projectRootPath);
        Directory.CreateDirectory(Path.Combine(projectRootPath, "01_Inbox"));

        FakeStatePersistenceService statePersistenceService = new()
        {
            AppState = new AppState
            {
                LastMasterRootDirectory = temporaryDirectory.DirectoryPath,
            },
        };

        FileSystemService fileSystemService = new();
        ProjectService projectService = new(fileSystemService, statePersistenceService);

        Project project = await projectService.CreateProjectAsync("My Dataset", projectRootPath);

        Assert.That(Guid.TryParse(project.Id, out _), Is.True);
        Assert.That(project.Name, Is.EqualTo("My Dataset"));
        Assert.That(project.RootFolderPath, Is.EqualTo(projectRootPath));
        Assert.That(project.Stages.Select(stage => stage.FolderName), Is.EqualTo(new[] { "01_Inbox" }));
        Assert.That(project.State.ActiveStageFolderName, Is.EqualTo("01_Inbox"));
        Assert.That(File.Exists(Path.Combine(projectRootPath, ".datasetstudio.json")), Is.True);
    }
}
