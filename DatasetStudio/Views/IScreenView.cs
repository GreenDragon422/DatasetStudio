using Avalonia.Input;
using DatasetStudio.ViewModels;

namespace DatasetStudio.Views;

public interface IScreenView
{
    ScreenViewModelBase? ScreenViewModel { get; }

    bool TryHandleKey(KeyEventArgs eventArgs);
}
