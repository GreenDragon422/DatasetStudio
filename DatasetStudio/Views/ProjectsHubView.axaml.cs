using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using DatasetStudio.ViewModels;

namespace DatasetStudio.Views;

public partial class ProjectsHubView : ScreenViewBase<ProjectsHubViewModel>
{
    private bool hasInitialized;

    public ProjectsHubView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    protected override IReadOnlyList<ScreenShortcut> BuildScreenShortcuts()
    {
        ProjectsHubViewModel? viewModel = ViewModel;
        if (viewModel is null)
        {
            return Array.Empty<ScreenShortcut>();
        }

        return new ScreenShortcut[]
        {
            CreateShortcut(Key.O, KeyModifiers.Control, "Ctrl+O", "Browse", OpenBrowseShortcut, allowWhenTextInputFocused: true),
            CreateShortcut(
                Key.N,
                KeyModifiers.Control,
                "Ctrl+N",
                "New Project",
                () => viewModel.NewProjectCommand.Execute(null),
                allowWhenTextInputFocused: true),
            CreateShortcut(
                Key.Enter,
                KeyModifiers.None,
                "Enter",
                "Scan",
                () => _ = viewModel.ScanMasterRootCommand.ExecuteAsync(null),
                allowWhenTextInputFocused: true,
                isAvailable: () => IsControlOrDescendantFocused(MasterRootPathTextBox)),
        };
    }

    private async void OnBrowseMasterRootClick(object? sender, Avalonia.Interactivity.RoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        await BrowseMasterRootAsync().ConfigureAwait(true);
    }

    private async void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        if (hasInitialized)
        {
            return;
        }

        if (DataContext is not ProjectsHubViewModel projectsHubViewModel)
        {
            return;
        }

        hasInitialized = true;
        await projectsHubViewModel.LoadProjectsCommand.ExecuteAsync(null).ConfigureAwait(true);
    }

    private async Task BrowseMasterRootAsync()
    {
        if (DataContext is not ProjectsHubViewModel projectsHubViewModel)
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
                Title = "Select master root folder",
                AllowMultiple = false,
            }).ConfigureAwait(true);

        if (folders.Count == 0)
        {
            return;
        }

        string folderPath = folders[0].Path.LocalPath;
        projectsHubViewModel.MasterRootPath = folderPath;
        await projectsHubViewModel.ScanMasterRootCommand.ExecuteAsync(null).ConfigureAwait(true);
    }

    private void OpenBrowseShortcut()
    {
        _ = BrowseMasterRootAsync();
    }
}
