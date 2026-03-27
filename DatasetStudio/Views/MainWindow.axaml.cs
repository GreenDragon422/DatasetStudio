using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using DatasetStudio.ViewModels;

namespace DatasetStudio.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AddHandler(InputElement.KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);
    }

    public MainWindow(MainWindowViewModel mainWindowViewModel)
    {
        InitializeComponent();
        DataContext = mainWindowViewModel;
        AddHandler(InputElement.KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Handled)
        {
            return;
        }

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

        bool wasHandled = activeScreenView.TryHandleKey(eventArgs);
        _ = wasHandled;
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
