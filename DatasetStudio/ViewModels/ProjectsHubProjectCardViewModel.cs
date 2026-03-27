using DatasetStudio.Models;
using System;
using System.Windows.Input;

namespace DatasetStudio.ViewModels;

public sealed class ProjectsHubProjectCardViewModel
{
    public ProjectsHubProjectCardViewModel(
        Project project,
        string projectId,
        string name,
        string rootFolderPath,
        int imageCount,
        int taggedImageCount,
        ICommand openCommand)
    {
        Project = project ?? throw new ArgumentNullException(nameof(project));
        ProjectId = projectId;
        Name = name;
        RootFolderPath = rootFolderPath;
        ImageCount = imageCount;
        TaggedImageCount = taggedImageCount;
        OpenCommand = openCommand;
    }

    public Project Project { get; }

    public string ProjectId { get; }

    public string Name { get; }

    public string RootFolderPath { get; }

    public int ImageCount { get; }

    public int TaggedImageCount { get; }

    public ICommand OpenCommand { get; }

    public double TaggedPercentage
    {
        get
        {
            if (ImageCount <= 0)
            {
                return 0;
            }

            return (double)TaggedImageCount / ImageCount * 100.0;
        }
    }

    public string ImageCountText
    {
        get
        {
            return string.Format("{0} image{1}", ImageCount, ImageCount == 1 ? string.Empty : "s");
        }
    }

    public string TaggedPercentageText
    {
        get
        {
            return string.Format("{0:0}% tagged", TaggedPercentage);
        }
    }
}
