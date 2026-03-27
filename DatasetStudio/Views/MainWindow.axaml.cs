using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using DatasetStudio.ViewModels;

namespace DatasetStudio.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(MainWindowViewModel mainWindowViewModel)
    {
        InitializeComponent();
        DataContext = mainWindowViewModel;
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (DataContext is not MainWindowViewModel mainWindowViewModel)
        {
            return;
        }

        _ = sender;
        _ = mainWindowViewModel;

        IScreenView? activeScreenView = GetActiveScreenView(mainWindowViewModel);
        if (activeScreenView is null)
        {
            return;
        }

        _ = activeScreenView.TryHandleKey(eventArgs);
    }

    private IScreenView? GetActiveScreenView(MainWindowViewModel mainWindowViewModel)
    {
        ContentControl activeHost = mainWindowViewModel.IsConfigOpen ? ProjectConfigurationHost : CurrentViewHost;
        foreach (Visual visual in activeHost.GetVisualDescendants())
        {
            if (visual is IScreenView screenView)
            {
                return screenView;
            }
        }

        return null;
    }
}
