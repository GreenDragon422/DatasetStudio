using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using DatasetStudio.Messages;
using DatasetStudio.Models;
using DatasetStudio.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.ComponentModel;

namespace DatasetStudio.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private ScreenViewModelBase? observedShellViewModel;
    private readonly IServiceProvider serviceProvider;

    public MainWindowViewModel(IServiceProvider serviceProvider, INavigationService navigationService, IMessenger messenger)
        : base(messenger)
    {
        this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _ = navigationService ?? throw new ArgumentNullException(nameof(navigationService));

        messenger.Register<MainWindowViewModel, OpenProjectConfigurationRequestedMessage>(this, static (recipient, message) =>
        {
            recipient.OpenProjectConfiguration(message.Project);
        });

        HintText = "Foundation shell initialized.";
        StatusText = "Ready for screen wiring.";
    }

    [ObservableProperty]
    private ScreenViewModelBase? currentView;

    [ObservableProperty]
    private bool isConfigOpen;

    [ObservableProperty]
    private ScreenViewModelBase? projectConfigurationContent;

    partial void OnCurrentViewChanged(ScreenViewModelBase? value)
    {
        _ = value;
        UpdateObservedShellViewModel();
    }

    partial void OnIsConfigOpenChanged(bool value)
    {
        _ = value;
        UpdateObservedShellViewModel();
    }

    partial void OnProjectConfigurationContentChanged(ScreenViewModelBase? value)
    {
        _ = value;
        UpdateObservedShellViewModel();
    }

    public void OpenProjectConfiguration(ScreenViewModelBase content)
    {
        ProjectConfigurationContent = content;
        IsConfigOpen = true;
    }

    public void OpenProjectConfiguration(Project project)
    {
        ProjectConfigurationViewModel projectConfigurationViewModel = serviceProvider.GetRequiredService<ProjectConfigurationViewModel>();
        projectConfigurationViewModel.LoadProject(project);
        OpenProjectConfiguration(projectConfigurationViewModel);
    }

    public void CloseProjectConfiguration()
    {
        IsConfigOpen = false;
        ProjectConfigurationContent = null;
    }

    private void OnCurrentViewPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.PropertyName == nameof(ViewModelBase.HintText)
            || eventArgs.PropertyName == nameof(ViewModelBase.StatusText)
            || eventArgs.PropertyName == nameof(ViewModelBase.TopBarContent))
        {
            SyncShellFromActiveView();
        }
    }

    private ScreenViewModelBase? GetActiveShellViewModel()
    {
        if (IsConfigOpen && ProjectConfigurationContent is not null)
        {
            return ProjectConfigurationContent;
        }

        return CurrentView;
    }

    private void SyncShellFromActiveView()
    {
        ScreenViewModelBase? activeShellViewModel = GetActiveShellViewModel();
        if (activeShellViewModel is null)
        {
            TopBarContent = null;
            HintText = "No active screen.";
            StatusText = string.Empty;
            return;
        }

        TopBarContent = activeShellViewModel.TopBarContent;
        HintText = activeShellViewModel.HintText;
        StatusText = activeShellViewModel.StatusText;
    }

    private void UpdateObservedShellViewModel()
    {
        ScreenViewModelBase? activeShellViewModel = GetActiveShellViewModel();
        if (ReferenceEquals(observedShellViewModel, activeShellViewModel))
        {
            SyncShellFromActiveView();
            return;
        }

        if (observedShellViewModel is not null)
        {
            observedShellViewModel.PropertyChanged -= OnCurrentViewPropertyChanged;
        }

        observedShellViewModel = activeShellViewModel;

        if (observedShellViewModel is not null)
        {
            observedShellViewModel.PropertyChanged += OnCurrentViewPropertyChanged;
        }

        SyncShellFromActiveView();
    }
}
