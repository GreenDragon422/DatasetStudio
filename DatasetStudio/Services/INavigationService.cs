using DatasetStudio.ViewModels;

namespace DatasetStudio.Services;

public interface INavigationService
{
    void NavigateTo<TViewModel>()
        where TViewModel : ViewModelBase;

    void NavigateTo<TViewModel>(object parameter)
        where TViewModel : ViewModelBase;

    void GoBack();
}
