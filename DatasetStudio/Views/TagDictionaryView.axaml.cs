using System;
using Avalonia.Controls;
using Avalonia.Input;
using DatasetStudio.ViewModels;
using System.Threading.Tasks;

namespace DatasetStudio.Views;

public partial class TagDictionaryView : UserControl
{
    public TagDictionaryView()
    {
        InitializeComponent();
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
}
