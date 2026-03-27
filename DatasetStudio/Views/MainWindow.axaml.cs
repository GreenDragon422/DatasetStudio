using Avalonia.Controls;
using Avalonia.Input;
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
        _ = eventArgs;
        _ = mainWindowViewModel;
        // Keyboard routing is intentionally stubbed here and will be wired in the integration phase.
    }
}
