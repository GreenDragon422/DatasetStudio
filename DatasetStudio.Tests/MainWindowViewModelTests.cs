using CommunityToolkit.Mvvm.Messaging;
using DatasetStudio.Tests.TestDoubles;
using DatasetStudio.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace DatasetStudio.Tests;

[TestFixture]
public class MainWindowViewModelTests
{
    [Test]
    public void CurrentView_SyncsShellState_AndTracksPropertyChanges()
    {
        StrongReferenceMessenger messenger = new();
        ServiceProvider serviceProvider = new ServiceCollection().BuildServiceProvider();
        MainWindowViewModel mainWindowViewModel = new(serviceProvider, new StubNavigationService(), messenger);
        TestScreenViewModel screenViewModel = new(messenger)
        {
            HintText = "Ctrl+N New Project",
            StatusText = "Projects Hub ready.",
            TopBarContent = "Projects",
        };

        mainWindowViewModel.CurrentView = screenViewModel;

        Assert.That(mainWindowViewModel.HintText, Is.EqualTo("Ctrl+N New Project"));
        Assert.That(mainWindowViewModel.StatusText, Is.EqualTo("Projects Hub ready."));
        Assert.That(mainWindowViewModel.TopBarContent, Is.EqualTo("Projects"));

        screenViewModel.HintText = "Ctrl+O Browse";
        screenViewModel.StatusText = "Scanning";
        screenViewModel.TopBarContent = "Browse";

        Assert.That(mainWindowViewModel.HintText, Is.EqualTo("Ctrl+O Browse"));
        Assert.That(mainWindowViewModel.StatusText, Is.EqualTo("Scanning"));
        Assert.That(mainWindowViewModel.TopBarContent, Is.EqualTo("Browse"));
    }

    [Test]
    public void ModalScreen_BecomesActiveShellSource_AndClosingRestoresCurrentView()
    {
        StrongReferenceMessenger messenger = new();
        ServiceProvider serviceProvider = new ServiceCollection().BuildServiceProvider();
        MainWindowViewModel mainWindowViewModel = new(serviceProvider, new StubNavigationService(), messenger);
        TestScreenViewModel currentScreenViewModel = new(messenger)
        {
            HintText = "Ctrl+N New Project",
            StatusText = "Projects Hub ready.",
            TopBarContent = "Projects",
        };
        TestScreenViewModel modalScreenViewModel = new(messenger)
        {
            HintText = "Ctrl+S Save  |  Esc Cancel",
            StatusText = "Project configuration ready.",
            TopBarContent = "Configuration",
        };

        mainWindowViewModel.CurrentView = currentScreenViewModel;
        mainWindowViewModel.OpenProjectConfiguration(modalScreenViewModel);

        Assert.That(mainWindowViewModel.HintText, Is.EqualTo("Ctrl+S Save  |  Esc Cancel"));
        Assert.That(mainWindowViewModel.StatusText, Is.EqualTo("Project configuration ready."));
        Assert.That(mainWindowViewModel.TopBarContent, Is.EqualTo("Configuration"));

        currentScreenViewModel.HintText = "Ctrl+O Browse";
        currentScreenViewModel.StatusText = "Background change";

        Assert.That(mainWindowViewModel.HintText, Is.EqualTo("Ctrl+S Save  |  Esc Cancel"));
        Assert.That(mainWindowViewModel.StatusText, Is.EqualTo("Project configuration ready."));

        modalScreenViewModel.HintText = "Esc Leave Field  |  Ctrl+S Save";
        Assert.That(mainWindowViewModel.HintText, Is.EqualTo("Esc Leave Field  |  Ctrl+S Save"));

        mainWindowViewModel.CloseProjectConfiguration();

        Assert.That(mainWindowViewModel.HintText, Is.EqualTo("Ctrl+O Browse"));
        Assert.That(mainWindowViewModel.StatusText, Is.EqualTo("Background change"));
        Assert.That(mainWindowViewModel.TopBarContent, Is.EqualTo("Projects"));
    }
}
