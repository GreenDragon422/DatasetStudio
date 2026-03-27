using DatasetStudio.Models;
using DatasetStudio.Services;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace DatasetStudio.Tests;

[TestFixture]
public class StatePersistenceServiceTests
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

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

    [Test]
    public async Task SaveAndLoadAppStateAsync_PreservesWindowAndProjectSettings()
    {
        using TemporaryDirectory temporaryDirectory = new();
        FileSystemService fileSystemService = new();
        StatePersistenceService statePersistenceService = new(fileSystemService, temporaryDirectory.DirectoryPath, TimeSpan.FromMilliseconds(20));
        AppState expectedState = new()
        {
            LastOpenedProjectId = "project-1",
            WindowWidth = 1280,
            WindowHeight = 900,
            WindowX = 100,
            WindowY = 80,
            LastMasterRootDirectory = Path.Combine(temporaryDirectory.DirectoryPath, "datasets"),
        };

        await statePersistenceService.SaveAppStateAsync(expectedState);
        AppState actualState = await statePersistenceService.LoadAppStateAsync();

        Assert.That(actualState.LastOpenedProjectId, Is.EqualTo(expectedState.LastOpenedProjectId));
        Assert.That(actualState.WindowWidth, Is.EqualTo(expectedState.WindowWidth));
        Assert.That(actualState.WindowHeight, Is.EqualTo(expectedState.WindowHeight));
        Assert.That(actualState.WindowX, Is.EqualTo(expectedState.WindowX));
        Assert.That(actualState.WindowY, Is.EqualTo(expectedState.WindowY));
        Assert.That(actualState.LastMasterRootDirectory, Is.EqualTo(expectedState.LastMasterRootDirectory));
    }

    [Test]
    public async Task SaveAndLoadProjectStateAsync_PreservesProjectStateBlock()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string masterRootPath = temporaryDirectory.DirectoryPath;
        string projectRootPath = Path.Combine(masterRootPath, "ProjectOne");
        string projectConfigurationPath = Path.Combine(projectRootPath, ".datasetstudio.json");
        Directory.CreateDirectory(projectRootPath);

        Project project = new()
        {
            Id = "project-1",
            Name = "ProjectOne",
            RootFolderPath = projectRootPath,
            State = new ProjectState
            {
                ActiveStageFolderName = "01_Inbox",
                ZoomSliderValue = 160,
                SelectedAiModelName = "old-model",
                LastInspectedImagePath = null,
            },
        };

        await File.WriteAllTextAsync(projectConfigurationPath, JsonSerializer.Serialize(project, JsonSerializerOptions));

        FileSystemService fileSystemService = new();
        StatePersistenceService statePersistenceService = new(fileSystemService, temporaryDirectory.DirectoryPath, TimeSpan.FromMilliseconds(20));
        await statePersistenceService.SaveAppStateAsync(new AppState { LastMasterRootDirectory = masterRootPath });

        ProjectState expectedState = new()
        {
            ActiveStageFolderName = "02_Review",
            ZoomSliderValue = 240,
            SelectedAiModelName = "wd14-vit-v2",
            LastInspectedImagePath = Path.Combine(projectRootPath, "02_Review", "image.png"),
        };

        await statePersistenceService.SaveProjectStateAsync(project.Id, expectedState);
        ProjectState actualState = await statePersistenceService.LoadProjectStateAsync(project.Id);

        Assert.That(actualState.ActiveStageFolderName, Is.EqualTo(expectedState.ActiveStageFolderName));
        Assert.That(actualState.ZoomSliderValue, Is.EqualTo(expectedState.ZoomSliderValue));
        Assert.That(actualState.SelectedAiModelName, Is.EqualTo(expectedState.SelectedAiModelName));
        Assert.That(actualState.LastInspectedImagePath, Is.EqualTo(expectedState.LastInspectedImagePath));
    }

    [Test]
    public async Task LoadAppStateAsync_WhenSettingsFileIsMissing_ReturnsDefaultState()
    {
        using TemporaryDirectory temporaryDirectory = new();
        FileSystemService fileSystemService = new();
        StatePersistenceService statePersistenceService = new(fileSystemService, temporaryDirectory.DirectoryPath, TimeSpan.FromMilliseconds(20));

        AppState appState = await statePersistenceService.LoadAppStateAsync();

        Assert.That(appState.LastOpenedProjectId, Is.Null);
        Assert.That(appState.LastMasterRootDirectory, Is.Null);
        Assert.That(appState.WindowWidth, Is.EqualTo(0));
        Assert.That(appState.WindowHeight, Is.EqualTo(0));
    }

    [Test]
    public async Task LoadProjectStateAsync_WhenStateBlockIsMissing_ReturnsDefaultState()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string masterRootPath = temporaryDirectory.DirectoryPath;
        string projectRootPath = Path.Combine(masterRootPath, "ProjectOne");
        string projectConfigurationPath = Path.Combine(projectRootPath, ".datasetstudio.json");
        Directory.CreateDirectory(projectRootPath);

        await File.WriteAllTextAsync(projectConfigurationPath, """
{
  "id": "project-1",
  "name": "ProjectOne",
  "prefixTags": [],
  "stages": []
}
""");

        FileSystemService fileSystemService = new();
        StatePersistenceService statePersistenceService = new(fileSystemService, temporaryDirectory.DirectoryPath, TimeSpan.FromMilliseconds(20));
        await statePersistenceService.SaveAppStateAsync(new AppState { LastMasterRootDirectory = masterRootPath });

        ProjectState projectState = await statePersistenceService.LoadProjectStateAsync("project-1");

        Assert.That(projectState.ActiveStageFolderName, Is.Null);
        Assert.That(projectState.ZoomSliderValue, Is.EqualTo(160));
        Assert.That(projectState.SelectedAiModelName, Is.Null);
        Assert.That(projectState.LastInspectedImagePath, Is.Null);
    }

    [Test]
    public async Task FlushPendingSavesAsync_PersistsQueuedApplicationAndProjectStateImmediately()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string masterRootPath = temporaryDirectory.DirectoryPath;
        string projectRootPath = Path.Combine(masterRootPath, "ProjectOne");
        string projectConfigurationPath = Path.Combine(projectRootPath, ".datasetstudio.json");
        Directory.CreateDirectory(projectRootPath);

        Project project = new()
        {
            Id = "project-flush",
            Name = "ProjectOne",
            RootFolderPath = projectRootPath,
            State = new ProjectState
            {
                ActiveStageFolderName = "01_Inbox",
                ZoomSliderValue = 160,
                SelectedAiModelName = "base-model",
                LastInspectedImagePath = null,
            },
        };

        await File.WriteAllTextAsync(projectConfigurationPath, JsonSerializer.Serialize(project, JsonSerializerOptions));

        FileSystemService fileSystemService = new();
        StatePersistenceService statePersistenceService = new(fileSystemService, temporaryDirectory.DirectoryPath, TimeSpan.FromMinutes(5));
        Task initialSaveTask = statePersistenceService.SaveAppStateAsync(new AppState
        {
            LastMasterRootDirectory = masterRootPath,
        });
        await statePersistenceService.FlushPendingSavesAsync();
        await initialSaveTask;

        AppState expectedAppState = new()
        {
            LastOpenedProjectId = project.Id,
            LastMasterRootDirectory = masterRootPath,
            WindowWidth = 1440,
            WindowHeight = 900,
            WindowX = 32,
            WindowY = 48,
        };

        ProjectState expectedProjectState = new()
        {
            ActiveStageFolderName = "02_Review",
            ZoomSliderValue = 220,
            SelectedAiModelName = "wd-eva02-large",
            LastInspectedImagePath = Path.Combine(projectRootPath, "02_Review", "cat.png"),
        };

        Task saveAppTask = statePersistenceService.SaveAppStateAsync(expectedAppState);
        Task saveProjectTask = statePersistenceService.SaveProjectStateAsync(project.Id, expectedProjectState);

        for (int attempt = 0; attempt < 100; attempt++)
        {
            ProjectState pendingProjectState = await statePersistenceService.LoadProjectStateAsync(project.Id);
            if (pendingProjectState.SelectedAiModelName == expectedProjectState.SelectedAiModelName)
            {
                break;
            }

            await Task.Delay(10);
        }

        await statePersistenceService.FlushPendingSavesAsync();
        await Task.WhenAll(saveAppTask, saveProjectTask);

        AppState actualAppState = await statePersistenceService.LoadAppStateAsync();
        ProjectState actualProjectState = await statePersistenceService.LoadProjectStateAsync(project.Id);

        Assert.That(actualAppState.LastOpenedProjectId, Is.EqualTo(expectedAppState.LastOpenedProjectId));
        Assert.That(actualAppState.WindowWidth, Is.EqualTo(expectedAppState.WindowWidth));
        Assert.That(actualAppState.WindowHeight, Is.EqualTo(expectedAppState.WindowHeight));
        Assert.That(actualAppState.WindowX, Is.EqualTo(expectedAppState.WindowX));
        Assert.That(actualAppState.WindowY, Is.EqualTo(expectedAppState.WindowY));
        Assert.That(actualProjectState.ActiveStageFolderName, Is.EqualTo(expectedProjectState.ActiveStageFolderName));
        Assert.That(actualProjectState.ZoomSliderValue, Is.EqualTo(expectedProjectState.ZoomSliderValue));
        Assert.That(actualProjectState.SelectedAiModelName, Is.EqualTo(expectedProjectState.SelectedAiModelName));
        Assert.That(actualProjectState.LastInspectedImagePath, Is.EqualTo(expectedProjectState.LastInspectedImagePath));
    }
}
