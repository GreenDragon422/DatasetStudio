using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.Messaging;
using DatasetStudio.Models;
using DatasetStudio.Services;
using DatasetStudio.ViewModels;
using DatasetStudio.Views;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DatasetStudio;

public partial class App : Application
{
    public IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (BindingPlugins.DataValidators.Count > 0)
            {
                BindingPlugins.DataValidators.RemoveAt(0);
            }

            ServiceCollection services = new();
            ConfigureServices(services);
            Services = services.BuildServiceProvider();

            NavigationService navigationService = Services.GetRequiredService<NavigationService>();
            IProjectService projectService = Services.GetRequiredService<IProjectService>();
            IStatePersistenceService statePersistenceService = Services.GetRequiredService<IStatePersistenceService>();
            MainWindowViewModel mainWindowViewModel = Services.GetRequiredService<MainWindowViewModel>();
            navigationService.Initialize(mainWindowViewModel);
            AppState appState = await statePersistenceService.LoadAppStateAsync().ConfigureAwait(true);
            MainWindow mainWindow = new(mainWindowViewModel);
            RestoreWindowGeometry(mainWindow, appState);
            WireWindowStatePersistence(mainWindow, statePersistenceService);
            desktop.MainWindow = mainWindow;
            await NavigateToStartupViewAsync(navigationService, projectService, appState).ConfigureAwait(true);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);
        services.AddSingleton<IFileSystemService, FileSystemService>();
        services.AddSingleton<ITagFileService, TagFileService>();
        services.AddSingleton<IStatePersistenceService, StatePersistenceService>();
        services.AddSingleton<IProjectService, ProjectService>();
        services.AddSingleton<IThumbnailCacheService, ThumbnailCacheService>();
        services.AddSingleton<ITagDictionaryService, TagDictionaryService>();
        services.AddSingleton<IClipboardService, ClipboardService>();
        services.AddSingleton<IAiTaggerService, AiTaggerService>();
        services.AddSingleton<BatchTagOperationService>();
        services.AddSingleton<NavigationService>();
        services.AddSingleton<INavigationService>(serviceProvider => serviceProvider.GetRequiredService<NavigationService>());
        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<ProjectsHubViewModel>();
        services.AddTransient<ProjectConfigurationViewModel>();
        services.AddTransient<LibraryGridViewModel>();
        services.AddTransient<InspectorModeViewModel>();
        services.AddTransient<TagDictionaryViewModel>();
    }

    private static async Task NavigateToStartupViewAsync(
        NavigationService navigationService,
        IProjectService projectService,
        AppState appState)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(appState.LastOpenedProjectId))
            {
                IReadOnlyList<Project> projects = await projectService.LoadProjectsAsync().ConfigureAwait(true);
                Project? startupProject = projects.FirstOrDefault(project =>
                    string.Equals(project.Id, appState.LastOpenedProjectId, StringComparison.OrdinalIgnoreCase));

                if (startupProject is not null)
                {
                    navigationService.NavigateTo<LibraryGridViewModel>(startupProject);
                    return;
                }
            }
        }
        catch
        {
        }

        navigationService.NavigateTo<ProjectsHubViewModel>(string.Empty);
    }

    private static void RestoreWindowGeometry(MainWindow mainWindow, AppState appState)
    {
        if (appState.WindowWidth > 0)
        {
            mainWindow.Width = appState.WindowWidth;
        }

        if (appState.WindowHeight > 0)
        {
            mainWindow.Height = appState.WindowHeight;
        }

        if (appState.WindowX >= 0 && appState.WindowY >= 0)
        {
            mainWindow.Position = new PixelPoint(
                (int)Math.Round(appState.WindowX),
                (int)Math.Round(appState.WindowY));
        }
    }

    private static void WireWindowStatePersistence(MainWindow mainWindow, IStatePersistenceService statePersistenceService)
    {
        mainWindow.PositionChanged += async (_, _) =>
        {
            try
            {
                await QueueWindowGeometrySaveAsync(mainWindow, statePersistenceService).ConfigureAwait(true);
            }
            catch
            {
            }
        };

        mainWindow.PropertyChanged += async (_, eventArgs) =>
        {
            if (eventArgs.Property != Window.WidthProperty && eventArgs.Property != Window.HeightProperty)
            {
                return;
            }

            try
            {
                await QueueWindowGeometrySaveAsync(mainWindow, statePersistenceService).ConfigureAwait(true);
            }
            catch
            {
            }
        };

        mainWindow.Closing += (_, _) =>
        {
            try
            {
                AppState appState = statePersistenceService.LoadAppStateAsync().GetAwaiter().GetResult();
                appState.WindowWidth = mainWindow.Width;
                appState.WindowHeight = mainWindow.Height;
                appState.WindowX = mainWindow.Position.X;
                appState.WindowY = mainWindow.Position.Y;
                Task saveTask = statePersistenceService.SaveAppStateAsync(appState);
                statePersistenceService.FlushPendingSavesAsync().GetAwaiter().GetResult();
                saveTask.GetAwaiter().GetResult();
            }
            catch
            {
            }
        };
    }

    private static async Task QueueWindowGeometrySaveAsync(MainWindow mainWindow, IStatePersistenceService statePersistenceService)
    {
        AppState appState = await statePersistenceService.LoadAppStateAsync().ConfigureAwait(true);
        appState.WindowWidth = mainWindow.Width;
        appState.WindowHeight = mainWindow.Height;
        appState.WindowX = mainWindow.Position.X;
        appState.WindowY = mainWindow.Position.Y;
        await statePersistenceService.SaveAppStateAsync(appState).ConfigureAwait(true);
    }
}
