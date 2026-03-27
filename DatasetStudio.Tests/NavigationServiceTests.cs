using System;
using CommunityToolkit.Mvvm.Messaging;
using DatasetStudio.Services;
using DatasetStudio.Tests.TestDoubles;
using DatasetStudio.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace DatasetStudio.Tests;

[TestFixture]
public class NavigationServiceTests
{
    [Test]
    public void NavigateTo_WithParameter_SetsCurrentView_AndCallsNavigationAware()
    {
        StrongReferenceMessenger messenger = new();
        ServiceProvider serviceProvider = CreateServiceProvider(messenger);
        NavigationService navigationService = new(serviceProvider);
        MainWindowViewModel mainWindowViewModel = new(serviceProvider, new StubNavigationService(), messenger);
        object navigationParameter = new();

        navigationService.Initialize(mainWindowViewModel);
        navigationService.NavigateTo<TestScreenViewModel>(navigationParameter);

        Assert.That(mainWindowViewModel.CurrentView, Is.TypeOf<TestScreenViewModel>());
        Assert.That(((TestScreenViewModel)mainWindowViewModel.CurrentView!).LastNavigationParameter, Is.SameAs(navigationParameter));
    }

    [Test]
    public void GoBack_RestoresPreviousScreenInstance()
    {
        StrongReferenceMessenger messenger = new();
        ServiceProvider serviceProvider = CreateServiceProvider(messenger);
        NavigationService navigationService = new(serviceProvider);
        MainWindowViewModel mainWindowViewModel = new(serviceProvider, new StubNavigationService(), messenger);

        navigationService.Initialize(mainWindowViewModel);
        navigationService.NavigateTo<TestScreenViewModel>();
        ScreenViewModelBase firstScreenViewModel = mainWindowViewModel.CurrentView ?? throw new InvalidOperationException("First screen should be assigned.");

        navigationService.NavigateTo<TestScreenViewModel>();
        ScreenViewModelBase secondScreenViewModel = mainWindowViewModel.CurrentView ?? throw new InvalidOperationException("Second screen should be assigned.");

        Assert.That(secondScreenViewModel, Is.Not.SameAs(firstScreenViewModel));

        navigationService.GoBack();

        Assert.That(mainWindowViewModel.CurrentView, Is.SameAs(firstScreenViewModel));
    }

    private static ServiceProvider CreateServiceProvider(StrongReferenceMessenger messenger)
    {
        ServiceCollection services = new();
        services.AddSingleton<IMessenger>(messenger);
        services.AddTransient<TestScreenViewModel>();
        return services.BuildServiceProvider();
    }
}
