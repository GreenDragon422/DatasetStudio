using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using DatasetStudio.ViewModels;

namespace DatasetStudio.Views;

public partial class ProjectConfigurationView : ScreenViewBase<ProjectConfigurationViewModel>
{
    private const string StageDragFormat = "application/x-datasetstudio-project-configuration-stage";

    private bool hasInitialized;

    public ProjectConfigurationView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    protected override IReadOnlyList<ScreenShortcut> BuildScreenShortcuts()
    {
        ProjectConfigurationViewModel? viewModel = ViewModel;
        if (viewModel is null)
        {
            return Array.Empty<ScreenShortcut>();
        }

        return new ScreenShortcut[]
        {
            CreateShortcut(Key.O, KeyModifiers.Control, "Ctrl+O", "Browse Root", OpenBrowseRootShortcut, allowWhenTextInputFocused: true),
            CreateShortcut(
                Key.S,
                KeyModifiers.Control,
                "Ctrl+S",
                "Save",
                () => viewModel.SaveCommand.Execute(null),
                allowWhenTextInputFocused: true,
                isAvailable: () => viewModel.CanSave),
            CreateShortcut(
                Key.Escape,
                KeyModifiers.None,
                "Esc",
                "Cancel",
                () => viewModel.CancelCommand.Execute(null),
                isAvailable: () => !HasEditableTextInputFocus()),
        };
    }

    private async void OnBrowseRootFolderClick(object? sender, Avalonia.Interactivity.RoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        await BrowseRootFolderAsync().ConfigureAwait(true);
    }

    private async void OnStageDragHandlePointerPressed(object? sender, PointerPressedEventArgs eventArgs)
    {
        if (sender is not Control control || control.DataContext is not ProjectConfigurationStageViewModel stageViewModel)
        {
            return;
        }

        if (!eventArgs.GetCurrentPoint(control).Properties.IsLeftButtonPressed)
        {
            return;
        }

        DataTransfer dataTransfer = new();
        dataTransfer.Add(DataTransferItem.Create(DataFormat.Text, stageViewModel.Order.ToString()));

        await DragDrop.DoDragDropAsync(eventArgs, dataTransfer, DragDropEffects.Move).ConfigureAwait(true);
    }

    private void OnStageDragOver(object? sender, DragEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.DataTransfer.TryGetText() is not null)
        {
            eventArgs.DragEffects = DragDropEffects.Move;
        }
        else
        {
            eventArgs.DragEffects = DragDropEffects.None;
        }
    }

    private void OnStageDrop(object? sender, DragEventArgs eventArgs)
    {
        if (sender is not Control targetControl || targetControl.DataContext is not ProjectConfigurationStageViewModel targetStage)
        {
            return;
        }

        if (eventArgs.DataTransfer.TryGetText() is not string orderText || !int.TryParse(orderText, out int sourceStageOrder))
        {
            return;
        }

        if (DataContext is not ProjectConfigurationViewModel viewModel)
        {
            return;
        }

        ProjectConfigurationStageViewModel? sourceStage = null;
        foreach (ProjectConfigurationStageViewModel stageViewModel in viewModel.Stages)
        {
            if (stageViewModel.Order == sourceStageOrder)
            {
                sourceStage = stageViewModel;
                break;
            }
        }

        if (sourceStage is null)
        {
            return;
        }

        viewModel.MoveStageBefore(sourceStage, targetStage);
        eventArgs.DragEffects = DragDropEffects.Move;
    }

    private void OnStageRowAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs eventArgs)
    {
        _ = eventArgs;

        if (sender is Border border)
        {
            DragDrop.AddDragOverHandler(border, OnStageDragOver);
            DragDrop.AddDropHandler(border, OnStageDrop);
        }
    }

    private void OnStageRowDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs eventArgs)
    {
        _ = eventArgs;

        if (sender is Border border)
        {
            DragDrop.RemoveDragOverHandler(border, OnStageDragOver);
            DragDrop.RemoveDropHandler(border, OnStageDrop);
        }
    }

    private async void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        if (hasInitialized)
        {
            return;
        }

        if (DataContext is not ProjectConfigurationViewModel viewModel)
        {
            return;
        }

        hasInitialized = true;
        viewModel.CloseRequested += OnCloseRequested;
        await viewModel.LoadAiModelsCommand.ExecuteAsync(null).ConfigureAwait(true);
    }

    private void OnCloseRequested(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        Window? window = TopLevel.GetTopLevel(this) as Window;
        if (window?.DataContext is MainWindowViewModel mainWindowViewModel)
        {
            mainWindowViewModel.CloseProjectConfiguration();
        }
    }

    private async Task BrowseRootFolderAsync()
    {
        if (DataContext is not ProjectConfigurationViewModel viewModel)
        {
            return;
        }

        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
        {
            return;
        }

        IReadOnlyList<IStorageFolder> folders = await topLevel.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = "Select project root folder",
                AllowMultiple = false,
            }).ConfigureAwait(true);

        if (folders.Count == 0)
        {
            return;
        }

        string folderPath = folders[0].Path.LocalPath;
        viewModel.BrowseRootFolderCommand.Execute(folderPath);
    }

    private void OpenBrowseRootShortcut()
    {
        _ = BrowseRootFolderAsync();
    }
}
