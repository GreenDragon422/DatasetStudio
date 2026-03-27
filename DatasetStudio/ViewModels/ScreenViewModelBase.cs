using CommunityToolkit.Mvvm.Messaging;

namespace DatasetStudio.ViewModels;

public abstract partial class ScreenViewModelBase : ViewModelBase
{
    protected ScreenViewModelBase(IMessenger messenger)
        : base(messenger)
    {
    }

    public virtual void OnScreenActivated()
    {
    }

    public virtual void OnScreenDeactivated()
    {
    }
}
