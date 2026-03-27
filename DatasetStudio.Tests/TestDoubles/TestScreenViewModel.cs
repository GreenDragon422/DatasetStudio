using CommunityToolkit.Mvvm.Messaging;
using DatasetStudio.Services;
using DatasetStudio.ViewModels;

namespace DatasetStudio.Tests.TestDoubles;

public sealed class TestScreenViewModel : ScreenViewModelBase, INavigationAware
{
    public TestScreenViewModel(IMessenger messenger)
        : base(messenger)
    {
    }

    public object? LastNavigationParameter { get; private set; }

    public int ActivationCount { get; private set; }

    public int DeactivationCount { get; private set; }

    public void OnNavigatedTo(object parameter)
    {
        LastNavigationParameter = parameter;
    }

    public override void OnScreenActivated()
    {
        ActivationCount++;
    }

    public override void OnScreenDeactivated()
    {
        DeactivationCount++;
    }
}
