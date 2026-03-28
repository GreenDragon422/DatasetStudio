using CommunityToolkit.Mvvm.ComponentModel;
using DatasetStudio.Models;
using System;

namespace DatasetStudio.ViewModels;

public partial class ProjectOverviewStageViewModel : ObservableObject
{
    public ProjectOverviewStageViewModel(WorkflowStage stage, string folderPath, int imageCount)
    {
        Stage = stage ?? throw new ArgumentNullException(nameof(stage));
        FolderPath = folderPath ?? throw new ArgumentNullException(nameof(folderPath));
        ImageCount = imageCount;
    }

    public WorkflowStage Stage { get; }

    public string DisplayName
    {
        get
        {
            return Stage.DisplayName;
        }
    }

    public string FolderName
    {
        get
        {
            return Stage.FolderName;
        }
    }

    public string FolderPath { get; }

    [ObservableProperty]
    private int imageCount;
}
