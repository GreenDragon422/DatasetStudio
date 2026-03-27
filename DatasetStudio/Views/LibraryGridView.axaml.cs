using System.Collections.Generic;
using Avalonia.Input;
using Avalonia.Interactivity;
using DatasetStudio.ViewModels;

namespace DatasetStudio.Views;

public partial class LibraryGridView : ScreenViewBase<LibraryGridViewModel>
{
    public LibraryGridView()
    {
        InitializeComponent();
    }

    protected override IReadOnlyList<ScreenShortcut> BuildScreenShortcuts()
    {
        return new ScreenShortcut[]
        {
            CreateSlashShortcut("Filter", FocusFilterTextBox),
            CreateShortcut(Key.Enter, KeyModifiers.None, "Enter", "Inspect", OpenFocusedImage, isAvailable: HasFocusedImage),
        };
    }

    private void FocusFilterTextBox()
    {
        FocusControl(FilterTextBox);
    }

    private void OpenFocusedImage()
    {
        ViewModel?.OpenInspectorCommand.Execute(null);
    }

    private bool HasFocusedImage()
    {
        return ViewModel is not null && ViewModel.FocusedImageIndex >= 0 && ViewModel.FocusedImageIndex < ViewModel.Images.Count;
    }

    private void OnThumbnailDoubleTapped(object? sender, RoutedEventArgs eventArgs)
    {
        _ = eventArgs;

        if (sender is not InputElement inputElement)
        {
            return;
        }

        if (inputElement.DataContext is not LibraryGridImageViewModel image)
        {
            return;
        }

        ViewModel?.OpenInspectorCommand.Execute(image);
    }
}
