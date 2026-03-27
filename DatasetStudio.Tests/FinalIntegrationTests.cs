using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using DatasetStudio.Models;
using DatasetStudio.Services;
using DatasetStudio.Tests.TestDoubles;
using DatasetStudio.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace DatasetStudio.Tests;

[TestFixture]
public sealed class FinalIntegrationTests
{
    [Test]
    public async Task NavigationFlow_ProjectsHub_ToReviewWorkspace_ToInspector_AndBack_PreservesNavigationAndState()
    {
        string workspacePath = Path.Combine(Path.GetTempPath(), "DatasetStudioFinalIntegrationTests", Guid.NewGuid().ToString("N"));
        string rootFolderPath = Path.Combine(workspacePath, "animal-study");
        string reviewFolderPath = Path.Combine(rootFolderPath, "02_Review");
        string rejectFolderPath = Path.Combine(rootFolderPath, "03_Reject");

        Directory.CreateDirectory(reviewFolderPath);
        Directory.CreateDirectory(rejectFolderPath);

        string catImagePath = Path.Combine(reviewFolderPath, "cat.png");
        string dogImagePath = Path.Combine(reviewFolderPath, "dog.png");

        await File.WriteAllBytesAsync(catImagePath, TinyPngBytes).ConfigureAwait(false);
        await File.WriteAllBytesAsync(dogImagePath, TinyPngBytes).ConfigureAwait(false);

        Project project = new Project
        {
            Id = "project-final-integration",
            Name = "Animal Study",
            RootFolderPath = rootFolderPath,
            PrefixTags = new List<string> { "dataset" },
            Stages = new List<WorkflowStage>
            {
                new WorkflowStage { Order = 1, FolderName = "02_Review", DisplayName = "Review" },
                new WorkflowStage { Order = 2, FolderName = "03_Reject", DisplayName = "Reject" },
            },
            TagDictionaryEntries = new List<TagDictionaryEntry>
            {
                new TagDictionaryEntry
                {
                    CanonicalName = "feline",
                    Aliases = new List<string> { "cat" },
                },
                new TagDictionaryEntry
                {
                    CanonicalName = "canine",
                    Aliases = new List<string> { "dog" },
                },
            },
            State = new ProjectState
            {
                ActiveStageFolderName = "02_Review",
                LastInspectedImagePath = catImagePath,
            },
        };

        StrongReferenceMessenger messenger = new StrongReferenceMessenger();
        FileSystemService fileSystemService = new FileSystemService();
        TagFileService tagFileService = new TagFileService();
        InMemoryProjectService projectService = new InMemoryProjectService(new[] { project });
        TagDictionaryService tagDictionaryService = new TagDictionaryService(projectService, tagFileService, messenger);
        RecordingClipboardService clipboardService = new RecordingClipboardService();
        TestAiTaggerService aiTaggerService = new TestAiTaggerService();
        TestStatePersistenceService statePersistenceService = new TestStatePersistenceService();
        statePersistenceService.SetAppState(new AppState
        {
            LastMasterRootDirectory = workspacePath,
            LastOpenedProjectId = null,
        });
        statePersistenceService.SetProjectState(project.Id, project.State);

        ServiceCollection services = new ServiceCollection();
        services.AddSingleton<IMessenger>(messenger);
        services.AddSingleton<IFileSystemService>(fileSystemService);
        services.AddSingleton<ITagFileService>(tagFileService);
        services.AddSingleton<IStatePersistenceService>(statePersistenceService);
        services.AddSingleton<IProjectService>(projectService);
        services.AddSingleton<IThumbnailCacheService, ThumbnailCacheService>();
        services.AddSingleton<ITagDictionaryService>(tagDictionaryService);
        services.AddSingleton(tagDictionaryService);
        services.AddSingleton<IClipboardService>(clipboardService);
        services.AddSingleton<IAiTaggerService>(aiTaggerService);
        services.AddSingleton<BatchTagOperationService>();
        services.AddSingleton<NavigationService>();
        services.AddSingleton<INavigationService>(serviceProvider => serviceProvider.GetRequiredService<NavigationService>());
        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<ProjectsHubViewModel>();
        services.AddTransient<LibraryGridViewModel>();
        services.AddTransient<InspectorModeViewModel>();

        ServiceProvider serviceProvider = services.BuildServiceProvider();

        try
        {
            NavigationService navigationService = serviceProvider.GetRequiredService<NavigationService>();
            MainWindowViewModel mainWindowViewModel = serviceProvider.GetRequiredService<MainWindowViewModel>();
            navigationService.Initialize(mainWindowViewModel);

            navigationService.NavigateTo<ProjectsHubViewModel>(string.Empty);
            await WaitForConditionAsync(() =>
            {
                if (mainWindowViewModel.CurrentView is not ProjectsHubViewModel currentProjectsHubViewModel)
                {
                    return false;
                }

                return currentProjectsHubViewModel.Projects.Count == 1;
            }).ConfigureAwait(false);

            ProjectsHubViewModel projectsHubViewModel = (ProjectsHubViewModel)mainWindowViewModel.CurrentView!;
            ProjectsHubProjectCardViewModel projectCard = projectsHubViewModel.Projects.Single();
            projectsHubViewModel.OpenProjectCommand.Execute(projectCard);

            await WaitForConditionAsync(() =>
            {
                if (mainWindowViewModel.CurrentView is not LibraryGridViewModel currentLibraryGridViewModel)
                {
                    return false;
                }

                return currentLibraryGridViewModel.Images.Count == 2
                    && currentLibraryGridViewModel.FocusedImageIndex >= 0;
            }).ConfigureAwait(false);

            AppState persistedAppState = statePersistenceService.GetAppState();
            Assert.That(persistedAppState.LastOpenedProjectId, Is.EqualTo(project.Id));

            LibraryGridViewModel libraryGridViewModel = (LibraryGridViewModel)mainWindowViewModel.CurrentView!;
            LibraryGridImageViewModel dogImage = libraryGridViewModel.Images.Single(image => string.Equals(image.FileName, "dog.png", StringComparison.Ordinal));
            libraryGridViewModel.OpenInspectorCommand.Execute(dogImage);

            await WaitForConditionAsync(() =>
            {
                if (mainWindowViewModel.CurrentView is not InspectorModeViewModel currentInspectorModeViewModel)
                {
                    return false;
                }

                return currentInspectorModeViewModel.CurrentImage is not null
                    && string.Equals(currentInspectorModeViewModel.CurrentImage.FilePath, dogImagePath, StringComparison.Ordinal);
            }).ConfigureAwait(false);

            ProjectState persistedProjectState = statePersistenceService.GetProjectState(project.Id);
            Assert.That(persistedProjectState.LastInspectedImagePath, Is.EqualTo(dogImagePath));

            navigationService.GoBack();
            Assert.That(mainWindowViewModel.CurrentView, Is.SameAs(libraryGridViewModel));
            Assert.That(libraryGridViewModel.Images[libraryGridViewModel.FocusedImageIndex].FilePath, Is.EqualTo(dogImagePath));

            navigationService.GoBack();
            Assert.That(mainWindowViewModel.CurrentView, Is.SameAs(projectsHubViewModel));
        }
        finally
        {
            serviceProvider.Dispose();

            if (Directory.Exists(workspacePath))
            {
                Directory.Delete(workspacePath, true);
            }
        }
    }

    private static async Task WaitForConditionAsync(Func<bool> condition)
    {
        for (int attempt = 0; attempt < 120; attempt++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(25).ConfigureAwait(false);
        }

        Assert.Fail("Condition was not met within the allotted time.");
    }

    private static readonly byte[] TinyPngBytes =
    {
        137, 80, 78, 71, 13, 10, 26, 10,
        0, 0, 0, 13, 73, 72, 68, 82, 0, 0, 0, 1, 0, 0, 0, 1, 8, 6, 0, 0, 0, 31, 21, 196, 137,
        0, 0, 0, 13, 73, 68, 65, 84, 120, 156, 99, 248, 207, 192, 240, 31, 0, 5, 0, 1, 255, 137, 153, 61, 29,
        0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130,
    };
}
