using Avalonia.Controls;
using DatasetStudio.ViewModels;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;

namespace DatasetStudio.Views;

public partial class ProjectsHubView : UserControl
{
    private bool hasInitialized;

    public ProjectsHubView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnBrowseMasterRootClick(object? sender, Avalonia.Interactivity.RoutedEventArgs eventArgs)
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

        IReadOnlyList<Avalonia.Platform.Storage.IStorageFolder> folders = await topLevel.StorageProvider.OpenFolderPickerAsync(
            new Avalonia.Platform.Storage.FolderPickerOpenOptions
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

    private async void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs eventArgs)
    {
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
}
