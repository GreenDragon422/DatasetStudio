using DatasetStudio.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;

namespace DatasetStudio.Services;

public class NavigationService : INavigationService
{
    private readonly Stack<ScreenViewModelBase> backStack = new();
    private readonly IServiceProvider serviceProvider;
    private MainWindowViewModel? mainWindowViewModel;

    public NavigationService(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }

    public void Initialize(MainWindowViewModel mainWindowViewModel)
    {
        this.mainWindowViewModel = mainWindowViewModel;
    }

    public void NavigateTo<TViewModel>()
        where TViewModel : ScreenViewModelBase
    {
        TViewModel nextViewModel = serviceProvider.GetRequiredService<TViewModel>();
        NavigateToCore(nextViewModel);
    }

    public void NavigateTo<TViewModel>(object parameter)
        where TViewModel : ScreenViewModelBase
    {
        TViewModel nextViewModel = serviceProvider.GetRequiredService<TViewModel>();

        if (nextViewModel is INavigationAware navigationAware)
        {
            navigationAware.OnNavigatedTo(parameter);
        }

        NavigateToCore(nextViewModel);
    }

    public void GoBack()
    {
        if (mainWindowViewModel is null)
        {
            return;
        }

        if (backStack.Count == 0)
        {
            return;
        }

        ScreenViewModelBase? currentViewModel = mainWindowViewModel.CurrentView;
        currentViewModel?.OnScreenDeactivated();

        ScreenViewModelBase previousViewModel = backStack.Pop();
        mainWindowViewModel.CurrentView = previousViewModel;
        previousViewModel.OnScreenActivated();
    }

    private void NavigateToCore(ScreenViewModelBase nextViewModel)
    {
        if (mainWindowViewModel is null)
        {
            throw new InvalidOperationException("NavigationService must be initialized with MainWindowViewModel before navigation can occur.");
        }

        if (mainWindowViewModel.CurrentView is not null)
        {
            mainWindowViewModel.CurrentView.OnScreenDeactivated();
            backStack.Push(mainWindowViewModel.CurrentView);
        }

        mainWindowViewModel.CurrentView = nextViewModel;
        nextViewModel.OnScreenActivated();
    }
}
