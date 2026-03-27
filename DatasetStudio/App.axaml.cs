using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.Messaging;
using DatasetStudio.Services;
using DatasetStudio.ViewModels;
using DatasetStudio.Views;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace DatasetStudio;

public partial class App : Application
{
    public IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
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
            MainWindowViewModel mainWindowViewModel = Services.GetRequiredService<MainWindowViewModel>();
            navigationService.Initialize(mainWindowViewModel);
            MainWindow mainWindow = new(mainWindowViewModel);
            desktop.MainWindow = mainWindow;
            navigationService.NavigateTo<ProjectsHubViewModel>();
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
        services.AddTransient<TagDictionaryViewModel>();
    }
}
