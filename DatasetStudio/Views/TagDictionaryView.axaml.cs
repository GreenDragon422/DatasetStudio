using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using DatasetStudio.ViewModels;

namespace DatasetStudio.Views;

public partial class TagDictionaryView : ScreenViewBase<TagDictionaryViewModel>
{
    public TagDictionaryView()
    {
        InitializeComponent();
    }

    protected override IReadOnlyList<ScreenShortcut> BuildScreenShortcuts()
    {
        TagDictionaryViewModel? viewModel = ViewModel;
        if (viewModel is null)
        {
            return Array.Empty<ScreenShortcut>();
        }

        return new ScreenShortcut[]
        {
            CreateSlashShortcut("Search", FocusSearchTextBox),
            CreateShortcut(Key.N, KeyModifiers.Control, "Ctrl+N", "New Tag", () => viewModel.NewTagCommand.Execute(null), allowWhenTextInputFocused: true),
            CreateShortcut(
                Key.F2,
                KeyModifiers.None,
                "F2",
                "Edit",
                BeginEditingSelectedEntry,
                isAvailable: () => viewModel.SelectedEntry is not null && !viewModel.IsEditing),
        };
    }

    private void OnEntryDoubleTapped(object? sender, TappedEventArgs eventArgs)
    {
        _ = eventArgs;

        if (DataContext is not TagDictionaryViewModel viewModel)
        {
            return;
        }

        if ((sender as Control)?.DataContext is not TagDictionaryRowViewModel rowViewModel)
        {
            return;
        }

        viewModel.BeginEditing(rowViewModel);
    }

    private void OnEditClick(object? sender, Avalonia.Interactivity.RoutedEventArgs eventArgs)
    {
        _ = eventArgs;

        if (DataContext is not TagDictionaryViewModel viewModel)
        {
            return;
        }

        if ((sender as Control)?.DataContext is not TagDictionaryRowViewModel rowViewModel)
        {
            return;
        }

        viewModel.BeginEditing(rowViewModel);
    }

    private async void OnSaveClick(object? sender, Avalonia.Interactivity.RoutedEventArgs eventArgs)
    {
        _ = eventArgs;
        await ExecuteRowActionAsync(sender, static (viewModel, rowViewModel) => viewModel.SaveEntryAsync(rowViewModel)).ConfigureAwait(true);
    }

    private void OnCancelClick(object? sender, Avalonia.Interactivity.RoutedEventArgs eventArgs)
    {
        _ = eventArgs;

        if (DataContext is not TagDictionaryViewModel viewModel)
        {
            return;
        }

        if ((sender as Control)?.DataContext is not TagDictionaryRowViewModel rowViewModel)
        {
            return;
        }

        viewModel.CancelEditing(rowViewModel);
    }

    private async void OnMergeClick(object? sender, Avalonia.Interactivity.RoutedEventArgs eventArgs)
    {
        _ = eventArgs;
        await ExecuteRowActionAsync(sender, static (viewModel, rowViewModel) => viewModel.BeginMergeAsync(rowViewModel)).ConfigureAwait(true);
    }

    private async void OnDeleteClick(object? sender, Avalonia.Interactivity.RoutedEventArgs eventArgs)
    {
        _ = eventArgs;
        await ExecuteRowActionAsync(sender, static (viewModel, rowViewModel) => viewModel.DeleteEntryAsync(rowViewModel, false)).ConfigureAwait(true);
    }

    private async void OnDeleteWithFilesClick(object? sender, Avalonia.Interactivity.RoutedEventArgs eventArgs)
    {
        _ = eventArgs;
        await ExecuteRowActionAsync(sender, static (viewModel, rowViewModel) => viewModel.DeleteEntryAsync(rowViewModel, true)).ConfigureAwait(true);
    }

    private void BeginEditingSelectedEntry()
    {
        if (ViewModel?.SelectedEntry is not TagDictionaryRowViewModel rowViewModel)
        {
            return;
        }

        ViewModel.BeginEditing(rowViewModel);
    }

    private async Task ExecuteRowActionAsync(object? sender, Func<TagDictionaryViewModel, TagDictionaryRowViewModel, Task> action)
    {
        if (DataContext is not TagDictionaryViewModel viewModel)
        {
            return;
        }

        if ((sender as Control)?.DataContext is not TagDictionaryRowViewModel rowViewModel)
        {
            return;
        }

        await action(viewModel, rowViewModel).ConfigureAwait(true);
    }

    private void FocusSearchTextBox()
    {
        FocusControl(SearchTextBox);
    }
}
