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
    private ViewModelBase? observedViewModel;
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
    private ViewModelBase? currentView;

    [ObservableProperty]
    private bool isConfigOpen;

    [ObservableProperty]
    private object? projectConfigurationContent;

    partial void OnCurrentViewChanged(ViewModelBase? value)
    {
        if (observedViewModel is not null)
        {
            observedViewModel.PropertyChanged -= OnCurrentViewPropertyChanged;
        }

        observedViewModel = value;

        if (observedViewModel is not null)
        {
            observedViewModel.PropertyChanged += OnCurrentViewPropertyChanged;
        }

        SyncShellFromCurrentView();
    }

    public void OpenProjectConfiguration(object content)
    {
        ProjectConfigurationContent = content;
        IsConfigOpen = true;
    }

    public void OpenProjectConfiguration(Project project)
    {
        ProjectConfigurationViewModel projectConfigurationViewModel = serviceProvider.GetRequiredService<ProjectConfigurationViewModel>();
        projectConfigurationViewModel.LoadProject(project);
        OpenProjectConfiguration(projectConfigurationViewModel);
        StatusText = string.Format("Configuring project {0}.", project.Name);
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
            SyncShellFromCurrentView();
        }
    }

    private void SyncShellFromCurrentView()
    {
        if (CurrentView is null)
        {
            TopBarContent = null;
            HintText = "No active screen.";
            StatusText = string.Empty;
            return;
        }

        TopBarContent = CurrentView.TopBarContent;
        HintText = CurrentView.HintText;
        StatusText = CurrentView.StatusText;
    }
}
