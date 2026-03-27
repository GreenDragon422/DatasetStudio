using DatasetStudio.Services;
using DatasetStudio.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Reflection;

namespace DatasetStudio.Tests;

[TestFixture]
public class DependencyInjectionTests
{
    [Test]
    public void ConfigureServices_ResolvesAllCorePhaseTwoServices()
    {
        MethodInfo configureServicesMethod = typeof(DatasetStudio.App).GetMethod("ConfigureServices", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("App.ConfigureServices was not found.");

        ServiceCollection services = new();
        configureServicesMethod.Invoke(null, new object[] { services });
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        Assert.That(serviceProvider.GetService<IFileSystemService>(), Is.Not.Null);
        Assert.That(serviceProvider.GetService<ITagFileService>(), Is.Not.Null);
        Assert.That(serviceProvider.GetService<IStatePersistenceService>(), Is.Not.Null);
        Assert.That(serviceProvider.GetService<IProjectService>(), Is.Not.Null);
        Assert.That(serviceProvider.GetService<IThumbnailCacheService>(), Is.Not.Null);
        Assert.That(serviceProvider.GetService<ITagDictionaryService>(), Is.Not.Null);
        Assert.That(serviceProvider.GetService<IClipboardService>(), Is.Not.Null);
        Assert.That(serviceProvider.GetService<IAiTaggerService>(), Is.Not.Null);
        Assert.That(serviceProvider.GetService<BatchTagOperationService>(), Is.Not.Null);
        Assert.That(serviceProvider.GetService<INavigationService>(), Is.Not.Null);
        Assert.That(serviceProvider.GetService<MainWindowViewModel>(), Is.Not.Null);
        Assert.That(serviceProvider.GetService<ProjectsHubViewModel>(), Is.Not.Null);
        Assert.That(serviceProvider.GetService<ProjectConfigurationViewModel>(), Is.Not.Null);
        Assert.That(serviceProvider.GetService<LibraryGridViewModel>(), Is.Not.Null);
        Assert.That(serviceProvider.GetService<TagDictionaryViewModel>(), Is.Not.Null);
    }
}
