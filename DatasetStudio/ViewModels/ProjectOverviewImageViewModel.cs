using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatasetStudio.Models;
using System;
using System.Collections.Generic;

namespace DatasetStudio.ViewModels;

public partial class ProjectOverviewImageViewModel : ObservableObject, IDisposable
{
    public ProjectOverviewImageViewModel(
        string filePath,
        string fileName,
        string tagFilePath,
        IReadOnlyList<string> tags,
        TagStatus status,
        Bitmap? thumbnail,
        Action<ProjectOverviewImageViewModel>? focusImageAction = null,
        Action<ProjectOverviewImageViewModel>? toggleSelectionAction = null)
    {
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        FileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
        TagFilePath = tagFilePath ?? throw new ArgumentNullException(nameof(tagFilePath));
        Tags = tags ?? throw new ArgumentNullException(nameof(tags));
        Status = status;
        Thumbnail = thumbnail;
        FocusCommand = new RelayCommand(() =>
        {
            if (focusImageAction is not null)
            {
                focusImageAction(this);
            }
        });
        ToggleSelectionCommand = new RelayCommand(() =>
        {
            if (toggleSelectionAction is not null)
            {
                toggleSelectionAction(this);
            }
        });
    }

    public string FilePath { get; }

    public string FileName { get; }

    public string TagFilePath { get; }

    [ObservableProperty]
    private IReadOnlyList<string> tags;

    [ObservableProperty]
    private TagStatus status;

    [ObservableProperty]
    private bool isSelected;

    [ObservableProperty]
    private bool isFocused;

    [ObservableProperty]
    private bool isAiProcessing;

    [ObservableProperty]
    private Bitmap? thumbnail;

    public string TagsPreview
    {
        get
        {
            return string.Join(", ", Tags);
        }
    }

    public bool HasThumbnail
    {
        get
        {
            return Thumbnail is not null;
        }
    }

    public IRelayCommand FocusCommand { get; }

    public IRelayCommand ToggleSelectionCommand { get; }

    partial void OnTagsChanged(IReadOnlyList<string> value)
    {
        _ = value;
        OnPropertyChanged(nameof(TagsPreview));
    }

    partial void OnThumbnailChanged(Bitmap? value)
    {
        _ = value;
        OnPropertyChanged(nameof(HasThumbnail));
    }

    public void Dispose()
    {
        Thumbnail?.Dispose();
    }
}
