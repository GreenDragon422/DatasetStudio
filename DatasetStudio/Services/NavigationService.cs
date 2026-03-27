using DatasetStudio.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;

namespace DatasetStudio.Services;

public class NavigationService : INavigationService
{
    private readonly Stack<ViewModelBase> backStack = new();
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
        where TViewModel : ViewModelBase
    {
        TViewModel nextViewModel = serviceProvider.GetRequiredService<TViewModel>();
        NavigateToCore(nextViewModel);
    }

    public void NavigateTo<TViewModel>(object parameter)
        where TViewModel : ViewModelBase
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

        mainWindowViewModel.CurrentView = backStack.Pop();
    }

    private void NavigateToCore(ViewModelBase nextViewModel)
    {
        if (mainWindowViewModel is null)
        {
            throw new InvalidOperationException("NavigationService must be initialized with MainWindowViewModel before navigation can occur.");
        }

        if (mainWindowViewModel.CurrentView is not null)
        {
            backStack.Push(mainWindowViewModel.CurrentView);
        }

        mainWindowViewModel.CurrentView = nextViewModel;
    }
}
