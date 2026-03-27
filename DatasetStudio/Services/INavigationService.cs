using DatasetStudio.ViewModels;

namespace DatasetStudio.Services;

public interface INavigationService
{
    void NavigateTo<TViewModel>()
        where TViewModel : ScreenViewModelBase;

    void NavigateTo<TViewModel>(object parameter)
        where TViewModel : ScreenViewModelBase;

    void GoBack();
}
