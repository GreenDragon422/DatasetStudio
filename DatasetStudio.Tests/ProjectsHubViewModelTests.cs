using CommunityToolkit.Mvvm.Messaging;
using DatasetStudio.Models;
using DatasetStudio.Services;
using DatasetStudio.Tests.TestDoubles;
using DatasetStudio.ViewModels;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;

namespace DatasetStudio.Tests;

[TestFixture]
public class ProjectsHubViewModelTests
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

    [Test]
    public async Task MasterRootPathChange_PersistsValidDirectoryToAppState()
    {
        using TemporaryDirectory temporaryDirectory = new();
        StrongReferenceMessenger messenger = new StrongReferenceMessenger();
        TestStatePersistenceService statePersistenceService = new TestStatePersistenceService();
        ProjectsHubViewModel viewModel = new ProjectsHubViewModel(
            new InMemoryProjectService(Array.Empty<Project>()),
            new FileSystemService(),
            new StubNavigationService(),
            messenger,
            statePersistenceService);

        try
        {
            viewModel.MasterRootPath = temporaryDirectory.DirectoryPath;
            await WaitForConditionAsync(() => statePersistenceService.AppSaveCount > 0).ConfigureAwait(false);

            AppState appState = statePersistenceService.GetAppState();
            Assert.That(appState.LastMasterRootDirectory, Is.EqualTo(temporaryDirectory.DirectoryPath));
        }
        finally
        {
            viewModel.Dispose();
        }
    }

    [Test]
    public async Task OpenProjectCommand_PersistsLastOpenedProjectId()
    {
        StrongReferenceMessenger messenger = new StrongReferenceMessenger();
        TestStatePersistenceService statePersistenceService = new TestStatePersistenceService();
        ProjectsHubViewModel viewModel = new ProjectsHubViewModel(
            new InMemoryProjectService(Array.Empty<Project>()),
            new FileSystemService(),
            new StubNavigationService(),
            messenger,
            statePersistenceService);

        Project project = new Project
        {
            Id = "project-open-1",
            Name = "Animals",
            RootFolderPath = Path.Combine("C:\\datasets", "animals"),
            State = new ProjectState
            {
                ActiveStageFolderName = "02_Review",
                ZoomSliderValue = 180,
            },
        };

        ProjectsHubProjectCardViewModel projectCard = new ProjectsHubProjectCardViewModel(
            project,
            project.Id,
            project.Name,
            project.RootFolderPath,
            10,
            7,
            new NoOpCommand());

        try
        {
            viewModel.OpenProjectCommand.Execute(projectCard);
            await WaitForConditionAsync(() => statePersistenceService.AppSaveCount > 0).ConfigureAwait(false);

            AppState appState = statePersistenceService.GetAppState();
            Assert.That(appState.LastOpenedProjectId, Is.EqualTo(project.Id));
        }
        finally
        {
            viewModel.Dispose();
        }
    }

    private static async Task WaitForConditionAsync(Func<bool> condition)
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(10).ConfigureAwait(false);
        }

        Assert.Fail("Condition was not met within the allotted time.");
    }

    private sealed class NoOpCommand : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add
            {
            }
            remove
            {
            }
        }

        public bool CanExecute(object? parameter)
        {
            _ = parameter;
            return true;
        }

        public void Execute(object? parameter)
        {
            _ = parameter;
        }
    }
}
