using System.Collections.Generic;
using Avalonia.Input;
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
        };
    }

    private void FocusFilterTextBox()
    {
        FocusControl(FilterTextBox);
    }
}
