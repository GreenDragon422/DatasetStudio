using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DatasetStudio.Messages;
using DatasetStudio.Models;
using DatasetStudio.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DatasetStudio.ViewModels;

public partial class LibraryGridViewModel : ScreenViewModelBase, INavigationAware
{
    private readonly IFileSystemService fileSystemService;
    private readonly ITagFileService tagFileService;
    private readonly ITagDictionaryService tagDictionaryService;
    private readonly IThumbnailCacheService thumbnailCacheService;
    private readonly IClipboardService clipboardService;
    private readonly INavigationService navigationService;
    private readonly IMessenger messenger;
    private readonly List<LibraryGridImageViewModel> allImages;

    private Project? currentProject;

    public LibraryGridViewModel(
        IFileSystemService fileSystemService,
        ITagFileService tagFileService,
        ITagDictionaryService tagDictionaryService,
        IThumbnailCacheService thumbnailCacheService,
        IClipboardService clipboardService,
        INavigationService navigationService,
        IMessenger messenger)
        : base(messenger)
    {
        this.fileSystemService = fileSystemService ?? throw new ArgumentNullException(nameof(fileSystemService));
        this.tagFileService = tagFileService ?? throw new ArgumentNullException(nameof(tagFileService));
        this.tagDictionaryService = tagDictionaryService ?? throw new ArgumentNullException(nameof(tagDictionaryService));
        this.thumbnailCacheService = thumbnailCacheService ?? throw new ArgumentNullException(nameof(thumbnailCacheService));
        this.clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
        this.navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        this.messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));

        allImages = new List<LibraryGridImageViewModel>();
        Stages = new ObservableCollection<LibraryGridStageViewModel>();
        Images = new ObservableCollection<LibraryGridImageViewModel>();
        SelectedImages = new ObservableCollection<LibraryGridImageViewModel>();
        AiModels = new ObservableCollection<AiModelInfo>();
        ZoomValue = 160;
        StatusText = "Open a project to load the library grid.";

        messenger.Register<LibraryGridViewModel, TagsChangedMessage>(this, static (recipient, message) =>
        {
            recipient.UpdateImageTags(message.ImagePath, message.NewTags, TagStatus.Ready);
        });
        messenger.Register<LibraryGridViewModel, ImageDeletedMessage>(this, static (recipient, message) =>
        {
            recipient.HandleImageDeleted(message);
        });
        messenger.Register<LibraryGridViewModel, ImageMovedMessage>(this, static (recipient, message) =>
        {
            recipient.HandleImageMoved(message);
        });
        messenger.Register<LibraryGridViewModel, AiTaggingCompletedMessage>(this, static (recipient, message) =>
        {
            recipient.UpdateImageTags(message.ImagePath, message.GeneratedTags, TagStatus.AutoTagged);
        });
    }

    [ObservableProperty]
    private ObservableCollection<LibraryGridStageViewModel> stages;

    [ObservableProperty]
    private LibraryGridStageViewModel? activeStage;

    [ObservableProperty]
    private ObservableCollection<LibraryGridImageViewModel> images;

    [ObservableProperty]
    private ObservableCollection<LibraryGridImageViewModel> selectedImages;

    [ObservableProperty]
    private int focusedImageIndex = -1;

    [ObservableProperty]
    private string filterText = string.Empty;

    [ObservableProperty]
    private int zoomValue;

    [ObservableProperty]
    private bool isBatchAddOpen;

    [ObservableProperty]
    private bool isBatchRemoveOpen;

    [ObservableProperty]
    private string projectName = string.Empty;

    [ObservableProperty]
    private ObservableCollection<AiModelInfo> aiModels;

    [ObservableProperty]
    private AiModelInfo? selectedAiModel;

    [ObservableProperty]
    private bool hasImages;

    public void OnNavigatedTo(object parameter)
    {
        if (parameter is not Project project)
        {
            StatusText = "Library Grid requires a project navigation parameter.";
            return;
        }

        currentProject = project;
        ProjectName = string.IsNullOrWhiteSpace(project.Name) ? Path.GetFileName(project.RootFolderPath) : project.Name;
        ZoomValue = project.State.ZoomSliderValue > 0 ? project.State.ZoomSliderValue : 160;
        LoadAiModelChoices(project);
        StatusText = string.Format("Loading library for {0}...", ProjectName);
        _ = LoadStagesCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task LoadStagesAsync()
    {
        if (currentProject is null)
        {
            return;
        }

        IReadOnlyList<WorkflowStage> workflowStages = GetWorkflowStages(currentProject);
        List<LibraryGridStageViewModel> stageViewModels = new List<LibraryGridStageViewModel>();

        foreach (WorkflowStage workflowStage in workflowStages)
        {
            string folderPath = GetStageFolderPath(currentProject, workflowStage);
            IReadOnlyList<string> imageFiles = await fileSystemService.GetImageFilesAsync(folderPath).ConfigureAwait(true);
            stageViewModels.Add(new LibraryGridStageViewModel(workflowStage, folderPath, imageFiles.Count));
        }

        Stages = new ObservableCollection<LibraryGridStageViewModel>(stageViewModels);

        LibraryGridStageViewModel? preferredStage = stageViewModels.FirstOrDefault(stage =>
            string.Equals(stage.FolderName, currentProject.State.ActiveStageFolderName, StringComparison.OrdinalIgnoreCase));
        ActiveStage = preferredStage ?? stageViewModels.FirstOrDefault();

        if (ActiveStage is null)
        {
            ReplaceImages(Array.Empty<LibraryGridImageViewModel>());
            StatusText = "No workflow stages were found for this project.";
            return;
        }

        StatusText = string.Format("Loaded {0} workflow stage{1}.", Stages.Count, Stages.Count == 1 ? string.Empty : "s");
    }

    [RelayCommand]
    private async Task SelectStageAsync(LibraryGridStageViewModel? stage)
    {
        if (stage is null || currentProject is null)
        {
            return;
        }

        currentProject.State.ActiveStageFolderName = stage.FolderName;
        StatusText = string.Format("Loading images from {0}...", stage.DisplayName);

        IReadOnlyList<string> imageFilePaths = await fileSystemService.GetImageFilesAsync(stage.FolderPath).ConfigureAwait(true);
        List<LibraryGridImageViewModel> imageViewModels = new List<LibraryGridImageViewModel>();

        foreach (string imageFilePath in imageFilePaths)
        {
            IReadOnlyList<string> fileTags = await tagFileService.ReadTagsAsync(tagFileService.GetTagFilePath(imageFilePath)).ConfigureAwait(true);
            IReadOnlyList<string> combinedTags = CombinePrefixAndFileTags(currentProject, fileTags);
            Bitmap? thumbnail = await LoadThumbnailAsync(imageFilePath).ConfigureAwait(true);
            TagStatus status = fileTags.Count == 0 ? TagStatus.Untagged : TagStatus.Ready;

            LibraryGridImageViewModel imageViewModel = new(
                imageFilePath,
                Path.GetFileName(imageFilePath),
                tagFileService.GetTagFilePath(imageFilePath),
                combinedTags,
                status,
                thumbnail);

            imageViewModels.Add(imageViewModel);
        }

        ReplaceImages(imageViewModels);
        StatusText = imageViewModels.Count == 0
            ? string.Format("{0} is empty.", stage.DisplayName)
            : string.Format("Loaded {0} image{1} from {2}.", imageViewModels.Count, imageViewModels.Count == 1 ? string.Empty : "s", stage.DisplayName);
    }

    [RelayCommand]
    private void FocusImage(LibraryGridImageViewModel? image)
    {
        if (image is null)
        {
            return;
        }

        int imageIndex = Images.IndexOf(image);
        if (imageIndex < 0)
        {
            return;
        }

        for (int index = 0; index < Images.Count; index++)
        {
            Images[index].IsFocused = index == imageIndex;
        }

        FocusedImageIndex = imageIndex;
        if (currentProject is not null)
        {
            currentProject.State.LastInspectedImagePath = image.FilePath;
        }

        StatusText = string.Format("Focused {0}.", image.FileName);
    }

    [RelayCommand]
    private void OpenInspector(LibraryGridImageViewModel? image)
    {
        if (currentProject is null)
        {
            return;
        }

        LibraryGridImageViewModel? targetImage = image;
        if (targetImage is null && FocusedImageIndex >= 0 && FocusedImageIndex < Images.Count)
        {
            targetImage = Images[FocusedImageIndex];
        }

        if (targetImage is null)
        {
            return;
        }

        FocusImage(targetImage);
        navigationService.NavigateTo<InspectorModeViewModel>(currentProject);
        StatusText = string.Format("Opening Inspector Mode for {0}.", targetImage.FileName);
    }

    [RelayCommand]
    private void ToggleSelection(LibraryGridImageViewModel? image)
    {
        if (image is null)
        {
            return;
        }

        image.IsSelected = !image.IsSelected;

        if (image.IsSelected)
        {
            if (!SelectedImages.Contains(image))
            {
                SelectedImages.Add(image);
            }
        }
        else
        {
            SelectedImages.Remove(image);
        }

        messenger.Send(new ImageSelectionChangedMessage(image.FilePath, image.IsSelected));
        StatusText = image.IsSelected
            ? string.Format("Selected {0}.", image.FileName)
            : string.Format("Deselected {0}.", image.FileName);
    }

    partial void OnActiveStageChanged(LibraryGridStageViewModel? value)
    {
        if (value is not null)
        {
            _ = SelectStageCommand.ExecuteAsync(value);
        }
    }

    partial void OnFilterTextChanged(string value)
    {
        _ = value;
        ApplyImageFilter();
    }

    private void ApplyImageFilter()
    {
        IEnumerable<LibraryGridImageViewModel> filteredImages = allImages;

        if (!string.IsNullOrWhiteSpace(FilterText))
        {
            filteredImages = filteredImages.Where(image =>
                image.FileName.Contains(FilterText, StringComparison.OrdinalIgnoreCase)
                || image.TagsPreview.Contains(FilterText, StringComparison.OrdinalIgnoreCase));
        }

        Images = new ObservableCollection<LibraryGridImageViewModel>(filteredImages);
        HasImages = Images.Count > 0;

        if (FocusedImageIndex >= Images.Count)
        {
            FocusedImageIndex = Images.Count - 1;
        }
    }

    private void ReplaceImages(IReadOnlyList<LibraryGridImageViewModel> imageViewModels)
    {
        foreach (LibraryGridImageViewModel existingImage in allImages)
        {
            existingImage.Dispose();
        }

        allImages.Clear();
        SelectedImages.Clear();
        FocusedImageIndex = -1;

        foreach (LibraryGridImageViewModel imageViewModel in imageViewModels)
        {
            allImages.Add(imageViewModel);
        }

        ApplyImageFilter();

        if (Images.Count > 0 && currentProject is not null)
        {
            LibraryGridImageViewModel? preferredImage = Images.FirstOrDefault(image =>
                string.Equals(image.FilePath, currentProject.State.LastInspectedImagePath, StringComparison.OrdinalIgnoreCase));
            FocusImage(preferredImage ?? Images[0]);
        }
        else if (Images.Count > 0)
        {
            FocusImage(Images[0]);
        }
    }

    private void LoadAiModelChoices(Project project)
    {
        AiModels.Clear();

        if (!string.IsNullOrWhiteSpace(project.AiModelName))
        {
            AiModelInfo aiModel = new AiModelInfo
            {
                Id = project.AiModelName,
                DisplayName = project.AiModelName,
                ModelPath = string.Empty,
            };

            AiModels.Add(aiModel);
            SelectedAiModel = aiModel;
        }
        else
        {
            SelectedAiModel = null;
        }
    }

    private IReadOnlyList<WorkflowStage> GetWorkflowStages(Project project)
    {
        if (Directory.Exists(project.RootFolderPath))
        {
            List<string> folderNames = Directory
                .EnumerateDirectories(project.RootFolderPath)
                .Select(Path.GetFileName)
                .Where(folderName => !string.IsNullOrWhiteSpace(folderName))
                .Cast<string>()
                .ToList();

            if (folderNames.Count > 0)
            {
                return WorkflowStageParser.ParseAndSort(folderNames);
            }
        }

        if (project.Stages.Count > 0)
        {
            return project.Stages
                .OrderBy(stage => stage.Order)
                .ThenBy(stage => stage.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return new List<WorkflowStage>
        {
            new WorkflowStage
            {
                Order = 0,
                FolderName = string.Empty,
                DisplayName = "Project Root",
            },
        };
    }

    private static string GetStageFolderPath(Project project, WorkflowStage workflowStage)
    {
        if (string.IsNullOrWhiteSpace(workflowStage.FolderName))
        {
            return project.RootFolderPath;
        }

        return Path.Combine(project.RootFolderPath, workflowStage.FolderName);
    }

    private IReadOnlyList<string> CombinePrefixAndFileTags(Project project, IReadOnlyList<string> fileTags)
    {
        List<string> combinedTags = project.PrefixTags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Concat(fileTags)
            .ToList();

        return combinedTags;
    }

    private async Task<Bitmap?> LoadThumbnailAsync(string imageFilePath)
    {
        try
        {
            using Stream thumbnailStream = await thumbnailCacheService.GetThumbnailAsync(imageFilePath, ZoomValue).ConfigureAwait(true);
            return new Bitmap(thumbnailStream);
        }
        catch
        {
            return null;
        }
    }

    private void UpdateImageTags(string imagePath, IReadOnlyList<string> fileTags, TagStatus status)
    {
        if (currentProject is null)
        {
            return;
        }

        LibraryGridImageViewModel? image = allImages.FirstOrDefault(candidate =>
            string.Equals(candidate.FilePath, imagePath, StringComparison.OrdinalIgnoreCase));

        if (image is null)
        {
            return;
        }

        image.Tags = CombinePrefixAndFileTags(currentProject, fileTags);
        image.Status = status;
        image.IsAiProcessing = false;
        ApplyImageFilter();
    }

    private void HandleImageDeleted(ImageDeletedMessage message)
    {
        LibraryGridImageViewModel? image = allImages.FirstOrDefault(candidate =>
            string.Equals(candidate.FilePath, message.ImagePath, StringComparison.OrdinalIgnoreCase));

        if (image is null)
        {
            return;
        }

        allImages.Remove(image);
        SelectedImages.Remove(image);
        image.Dispose();
        ApplyImageFilter();

        if (ActiveStage is not null && string.Equals(ActiveStage.FolderPath, message.FolderPath, StringComparison.OrdinalIgnoreCase))
        {
            ActiveStage.ImageCount = Math.Max(0, ActiveStage.ImageCount - 1);
        }
    }

    private void HandleImageMoved(ImageMovedMessage message)
    {
        if (ActiveStage is null)
        {
            return;
        }

        bool affectsActiveStage = string.Equals(ActiveStage.FolderPath, message.SourceFolder, StringComparison.OrdinalIgnoreCase)
            || string.Equals(ActiveStage.FolderPath, message.TargetFolder, StringComparison.OrdinalIgnoreCase);

        if (!affectsActiveStage)
        {
            return;
        }

        _ = LoadStagesCommand.ExecuteAsync(null);
    }
}
