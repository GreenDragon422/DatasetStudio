using DatasetStudio.Services;
using DatasetStudio.ViewModels;

namespace DatasetStudio.Tests.TestDoubles;

public sealed class StubNavigationService : INavigationService
{
    public void GoBack()
    {
    }

    public void NavigateTo<TViewModel>()
        where TViewModel : ScreenViewModelBase
    {
    }

    public void NavigateTo<TViewModel>(object parameter)
        where TViewModel : ScreenViewModelBase
    {
        _ = parameter;
    }
}
