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
using System.Threading;
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
    private readonly IAiTaggerService aiTaggerService;
    private readonly BatchTagOperationService batchTagOperationService;
    private readonly IMessenger messenger;
    private readonly IStatePersistenceService statePersistenceService;
    private readonly List<LibraryGridImageViewModel> allImages;
    private readonly object projectWatcherStateGate;

    private Project? currentProject;
    private FileSystemWatcher? projectWatcher;
    private CancellationTokenSource? projectWatcherRefreshCancellationSource;
    private bool isIgnoringInternalImageMovedMessages;
    private bool isRestoringPersistedProjectState;
    private HashSet<string> pendingThumbnailInvalidationPaths;

    public LibraryGridViewModel(
        IFileSystemService fileSystemService,
        ITagFileService tagFileService,
        ITagDictionaryService tagDictionaryService,
        IThumbnailCacheService thumbnailCacheService,
        IClipboardService clipboardService,
        INavigationService navigationService,
        IAiTaggerService aiTaggerService,
        BatchTagOperationService batchTagOperationService,
        IMessenger messenger,
        IStatePersistenceService statePersistenceService)
        : base(messenger)
    {
        this.fileSystemService = fileSystemService ?? throw new ArgumentNullException(nameof(fileSystemService));
        this.tagFileService = tagFileService ?? throw new ArgumentNullException(nameof(tagFileService));
        this.tagDictionaryService = tagDictionaryService ?? throw new ArgumentNullException(nameof(tagDictionaryService));
        this.thumbnailCacheService = thumbnailCacheService ?? throw new ArgumentNullException(nameof(thumbnailCacheService));
        this.clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
        this.navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        this.aiTaggerService = aiTaggerService ?? throw new ArgumentNullException(nameof(aiTaggerService));
        this.batchTagOperationService = batchTagOperationService ?? throw new ArgumentNullException(nameof(batchTagOperationService));
        this.messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        this.statePersistenceService = statePersistenceService ?? throw new ArgumentNullException(nameof(statePersistenceService));

        allImages = new List<LibraryGridImageViewModel>();
        projectWatcherStateGate = new object();
        pendingThumbnailInvalidationPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Stages = new ObservableCollection<LibraryGridStageViewModel>();
        Images = new ObservableCollection<LibraryGridImageViewModel>();
        ImageRows = new ObservableCollection<LibraryGridImageRowViewModel>();
        SelectedImages = new ObservableCollection<LibraryGridImageViewModel>();
        AiModels = new ObservableCollection<AiModelInfo>();
        BatchAddSuggestions = new ObservableCollection<BatchTagSuggestionViewModel>();
        BatchRemoveSuggestions = new ObservableCollection<BatchTagSuggestionViewModel>();
        ZoomValue = 160;
        StatusText = "Open a project to load Project Overview.";

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
        messenger.Register<LibraryGridViewModel, AiTaggingFailedMessage>(this, static (recipient, message) =>
        {
            recipient.HandleAiTaggingFailed(message);
        });
    }

    [ObservableProperty]
    private ObservableCollection<LibraryGridStageViewModel> stages;

    [ObservableProperty]
    private LibraryGridStageViewModel? activeStage;

    [ObservableProperty]
    private ObservableCollection<LibraryGridImageViewModel> images;

    [ObservableProperty]
    private ObservableCollection<LibraryGridImageRowViewModel> imageRows;

    [ObservableProperty]
    private ObservableCollection<LibraryGridImageViewModel> selectedImages;

    [ObservableProperty]
    private int focusedImageIndex = -1;

    [ObservableProperty]
    private string filterText = string.Empty;

    [ObservableProperty]
    private int zoomValue;

    [ObservableProperty]
    private int itemsPerRow = 1;

    [ObservableProperty]
    private bool isBatchAddOpen;

    [ObservableProperty]
    private bool isBatchRemoveOpen;

    [ObservableProperty]
    private string batchAddQueryText = string.Empty;

    [ObservableProperty]
    private string batchRemoveQueryText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<BatchTagSuggestionViewModel> batchAddSuggestions;

    [ObservableProperty]
    private ObservableCollection<BatchTagSuggestionViewModel> batchRemoveSuggestions;

    [ObservableProperty]
    private BatchTagSuggestionViewModel? selectedBatchAddSuggestion;

    [ObservableProperty]
    private BatchTagSuggestionViewModel? selectedBatchRemoveSuggestion;

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
            StatusText = "Project Overview requires a project navigation parameter.";
            return;
        }

        currentProject = project;
        ProjectName = string.IsNullOrWhiteSpace(project.Name) ? Path.GetFileName(project.RootFolderPath) : project.Name;
        StatusText = string.Format("Loading library for {0}...", ProjectName);
        _ = RestoreProjectStateAndLoadAsync(project);
    }

    public override void OnScreenActivated()
    {
        ConfigureProjectWatcher();
    }

    public override void OnScreenDeactivated()
    {
        DetachProjectWatcher();
    }

    [RelayCommand]
    private Task LoadStagesAsync()
    {
        return LoadStagesCoreAsync();
    }

    [RelayCommand]
    private Task SelectStageAsync(LibraryGridStageViewModel? stage)
    {
        return SelectStageCoreAsync(stage);
    }

    [RelayCommand]
    private void FocusImage(LibraryGridImageViewModel? image)
    {
        SetFocusedImage(image, updateStatusText: true);
    }

    [RelayCommand]
    private void OpenInspector(LibraryGridImageViewModel? image)
    {
        if (currentProject is null)
        {
            return;
        }

        LibraryGridImageViewModel? targetImage = image ?? GetFocusedImage();
        if (targetImage is null)
        {
            return;
        }

        SetFocusedImage(targetImage, updateStatusText: false);
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

    [RelayCommand]
    private void NavigateGrid(int offset)
    {
        if (Images.Count == 0)
        {
            return;
        }

        int currentIndex = FocusedImageIndex;
        if (currentIndex < 0)
        {
            SetFocusedImage(Images[0], updateStatusText: false);
            return;
        }

        int targetIndex = currentIndex + offset;
        if (targetIndex < 0)
        {
            targetIndex = 0;
        }

        if (targetIndex >= Images.Count)
        {
            targetIndex = Images.Count - 1;
        }

        SetFocusedImage(Images[targetIndex], updateStatusText: false);
    }

    private async Task LoadStagesCoreAsync()
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

    private async Task SelectStageCoreAsync(LibraryGridStageViewModel? stage)
    {
        if (stage is null || currentProject is null)
        {
            return;
        }

        CloseBatchAdd();
        CloseBatchRemove();
        currentProject.State.ActiveStageFolderName = stage.FolderName;
        if (!isRestoringPersistedProjectState)
        {
            _ = PersistCurrentProjectStateAsync();
        }

        StatusText = string.Format("Loading images from {0}...", stage.DisplayName);

        IReadOnlyList<string> imageFilePaths = await fileSystemService.GetImageFilesAsync(stage.FolderPath).ConfigureAwait(true);
        List<LibraryGridImageViewModel> imageViewModels = new List<LibraryGridImageViewModel>();

        foreach (string imageFilePath in imageFilePaths)
        {
            string tagFilePath = tagFileService.GetTagFilePath(imageFilePath);
            IReadOnlyList<string> fileTags = await tagFileService.ReadTagsAsync(tagFilePath).ConfigureAwait(true);
            IReadOnlyList<string> combinedTags = CombinePrefixAndFileTags(currentProject, fileTags);
            Bitmap? thumbnail = await LoadThumbnailAsync(imageFilePath).ConfigureAwait(true);
            TagStatus status = fileTags.Count == 0 ? TagStatus.Untagged : TagStatus.Ready;

            LibraryGridImageViewModel imageViewModel = new(
                imageFilePath,
                Path.GetFileName(imageFilePath),
                tagFilePath,
                combinedTags,
                status,
                thumbnail);
            imageViewModel.IsAiProcessing = aiTaggerService.IsProcessing(imageFilePath);

            imageViewModels.Add(imageViewModel);
        }

        ReplaceImages(imageViewModels);
        int queuedImageCount = QueueAiTaggingForImages(imageViewModels);
        StatusText = imageViewModels.Count == 0
            ? string.Format("{0} is empty.", stage.DisplayName)
            : string.Format("Loaded {0} image{1} from {2}.", imageViewModels.Count, imageViewModels.Count == 1 ? string.Empty : "s", stage.DisplayName);

        if (queuedImageCount > 0)
        {
            StatusText = string.Format(
                "{0} Queued AI tagging for {1} image{2}.",
                StatusText,
                queuedImageCount,
                queuedImageCount == 1 ? string.Empty : "s");
        }
    }

    [RelayCommand]
    private Task OpenBatchAddAsync()
    {
        return OpenBatchAddCoreAsync();
    }

    [RelayCommand]
    private void CloseBatchAdd()
    {
        IsBatchAddOpen = false;
        BatchAddQueryText = string.Empty;
        BatchAddSuggestions = new ObservableCollection<BatchTagSuggestionViewModel>();
        SelectedBatchAddSuggestion = null;
    }

    [RelayCommand]
    private Task OpenBatchRemoveAsync()
    {
        return OpenBatchRemoveCoreAsync();
    }

    [RelayCommand]
    private void CloseBatchRemove()
    {
        IsBatchRemoveOpen = false;
        BatchRemoveQueryText = string.Empty;
        BatchRemoveSuggestions = new ObservableCollection<BatchTagSuggestionViewModel>();
        SelectedBatchRemoveSuggestion = null;
    }

    [RelayCommand]
    private Task CommitBatchAddAsync()
    {
        return CommitBatchAddCoreAsync();
    }

    [RelayCommand]
    private Task CommitBatchRemoveAsync()
    {
        return CommitBatchRemoveCoreAsync();
    }

    [RelayCommand]
    private Task MoveImageAsync(int stageOffset)
    {
        return MoveImageCoreAsync(stageOffset);
    }

    [RelayCommand]
    private void NavigateStage(int stageOffset)
    {
        if (ActiveStage is null)
        {
            return;
        }

        int activeStageIndex = Stages.IndexOf(ActiveStage);
        int targetStageIndex = activeStageIndex + stageOffset;
        if (targetStageIndex < 0 || targetStageIndex >= Stages.Count)
        {
            return;
        }

        ActiveStage = Stages[targetStageIndex];
        StatusText = string.Format("Switching to {0}.", Stages[targetStageIndex].DisplayName);
    }

    [RelayCommand]
    private Task DeleteImageAsync()
    {
        return DeleteImageCoreAsync();
    }

    [RelayCommand]
    private Task CopyTagsAsync()
    {
        return CopyTagsCoreAsync();
    }

    [RelayCommand]
    private Task PasteTagsAsync()
    {
        return PasteTagsCoreAsync();
    }

    private async Task OpenBatchAddCoreAsync()
    {
        if (currentProject is null || GetBatchScopeImages().Count == 0)
        {
            StatusText = "Select or focus at least one image before batch-adding tags.";
            return;
        }

        CloseBatchRemove();
        IsBatchAddOpen = true;
        BatchAddQueryText = string.Empty;
        await RefreshBatchAddSuggestionsAsync(BatchAddQueryText).ConfigureAwait(true);
        StatusText = "Batch add is ready. Type a tag and press Enter.";
    }

    private async Task OpenBatchRemoveCoreAsync()
    {
        if (currentProject is null || GetBatchScopeImages().Count == 0)
        {
            StatusText = "Select or focus at least one image before batch-removing tags.";
            return;
        }

        CloseBatchAdd();
        IsBatchRemoveOpen = true;
        BatchRemoveQueryText = string.Empty;
        await RefreshBatchRemoveSuggestionsAsync(BatchRemoveQueryText).ConfigureAwait(true);
        StatusText = "Batch remove is ready. Type a tag and press Enter.";
    }

    private async Task CommitBatchAddCoreAsync()
    {
        if (currentProject is null)
        {
            return;
        }

        IReadOnlyList<LibraryGridImageViewModel> targetImages = GetBatchScopeImages();
        if (targetImages.Count == 0)
        {
            StatusText = "There are no images available for batch add.";
            return;
        }

        string tag = GetBatchTagInput(SelectedBatchAddSuggestion, BatchAddQueryText);
        if (string.IsNullOrWhiteSpace(tag))
        {
            StatusText = "Choose or type a tag to batch add.";
            return;
        }

        IReadOnlyList<string> targetImagePaths = targetImages.Select(image => image.FilePath).ToList();
        await batchTagOperationService.AddTagAsync(currentProject.Id, targetImagePaths, tag).ConfigureAwait(true);
        CloseBatchAdd();
        StatusText = string.Format(
            "Added {0} to {1} image{2}.",
            tagDictionaryService.ResolveAlias(currentProject.Id, tag),
            targetImagePaths.Count,
            targetImagePaths.Count == 1 ? string.Empty : "s");
    }

    private async Task CommitBatchRemoveCoreAsync()
    {
        if (currentProject is null)
        {
            return;
        }

        IReadOnlyList<LibraryGridImageViewModel> targetImages = GetBatchScopeImages();
        if (targetImages.Count == 0)
        {
            StatusText = "There are no images available for batch remove.";
            return;
        }

        string tag = GetBatchTagInput(SelectedBatchRemoveSuggestion, BatchRemoveQueryText);
        if (string.IsNullOrWhiteSpace(tag))
        {
            StatusText = "Choose or type a tag to batch remove.";
            return;
        }

        IReadOnlyList<string> targetImagePaths = targetImages.Select(image => image.FilePath).ToList();
        await batchTagOperationService.RemoveTagAsync(currentProject.Id, targetImagePaths, tag).ConfigureAwait(true);
        CloseBatchRemove();
        StatusText = string.Format(
            "Removed {0} from {1} image{2}.",
            tagDictionaryService.ResolveAlias(currentProject.Id, tag),
            targetImagePaths.Count,
            targetImagePaths.Count == 1 ? string.Empty : "s");
    }

    private async Task MoveImageCoreAsync(int stageOffset)
    {
        if (currentProject is null || ActiveStage is null)
        {
            return;
        }

        IReadOnlyList<LibraryGridImageViewModel> selectedImagesSnapshot = GetStrictSelectedImages();
        if (selectedImagesSnapshot.Count == 0)
        {
            StatusText = "Select one or more images before moving them.";
            return;
        }

        int activeStageIndex = Stages.IndexOf(ActiveStage);
        int targetStageIndex = activeStageIndex + stageOffset;
        if (targetStageIndex < 0 || targetStageIndex >= Stages.Count)
        {
            return;
        }

        LibraryGridStageViewModel targetStage = Stages[targetStageIndex];
        isIgnoringInternalImageMovedMessages = true;

        try
        {
            foreach (LibraryGridImageViewModel image in selectedImagesSnapshot)
            {
                await fileSystemService.MoveFileAsync(image.FilePath, targetStage.FolderPath).ConfigureAwait(true);

                if (tagFileService.TagFileExists(image.FilePath))
                {
                    await fileSystemService.MoveFileAsync(image.TagFilePath, targetStage.FolderPath).ConfigureAwait(true);
                }

                UpdateLastInspectedImagePathForMovedImage(image, targetStage.FolderPath);
                messenger.Send(new ImageMovedMessage(image.FilePath, ActiveStage.FolderPath, targetStage.FolderPath));
            }
        }
        finally
        {
            isIgnoringInternalImageMovedMessages = false;
        }

        await LoadStagesCoreAsync().ConfigureAwait(true);
        await PersistCurrentProjectStateAsync().ConfigureAwait(true);
        StatusText = string.Format(
            "Moved {0} image{1} to {2}.",
            selectedImagesSnapshot.Count,
            selectedImagesSnapshot.Count == 1 ? string.Empty : "s",
            targetStage.DisplayName);
    }

    private async Task DeleteImageCoreAsync()
    {
        IReadOnlyList<LibraryGridImageViewModel> selectedImagesSnapshot = GetStrictSelectedImages();
        if (selectedImagesSnapshot.Count == 0)
        {
            StatusText = "Select one or more images before deleting them.";
            return;
        }

        foreach (LibraryGridImageViewModel image in selectedImagesSnapshot)
        {
            string folderPath = Path.GetDirectoryName(image.FilePath) ?? string.Empty;
            await fileSystemService.RecycleFileAsync(image.FilePath).ConfigureAwait(true);

            if (tagFileService.TagFileExists(image.FilePath))
            {
                await fileSystemService.RecycleFileAsync(image.TagFilePath).ConfigureAwait(true);
            }

            if (currentProject is not null
                && string.Equals(currentProject.State.LastInspectedImagePath, image.FilePath, StringComparison.OrdinalIgnoreCase))
            {
                currentProject.State.LastInspectedImagePath = null;
            }

            messenger.Send(new ImageDeletedMessage(image.FilePath, folderPath));
        }

        await PersistCurrentProjectStateAsync().ConfigureAwait(true);
        StatusText = string.Format(
            "Sent {0} image{1} to the recycle bin.",
            selectedImagesSnapshot.Count,
            selectedImagesSnapshot.Count == 1 ? string.Empty : "s");
    }

    private async Task CopyTagsCoreAsync()
    {
        LibraryGridImageViewModel? focusedImage = GetFocusedImage();
        if (focusedImage is null)
        {
            return;
        }

        await clipboardService.CopyTagsAsync(focusedImage.Tags).ConfigureAwait(true);
        StatusText = string.Format("Copied tags from {0}.", focusedImage.FileName);
    }

    private async Task PasteTagsCoreAsync()
    {
        if (currentProject is null)
        {
            return;
        }

        LibraryGridImageViewModel? focusedImage = GetFocusedImage();
        if (focusedImage is null)
        {
            return;
        }

        IReadOnlyList<string> clipboardTags = await clipboardService.PasteTagsAsync().ConfigureAwait(true);
        if (clipboardTags.Count == 0)
        {
            StatusText = "Clipboard does not contain any tags.";
            return;
        }

        List<string> normalizedTags = NormalizeClipboardTags(clipboardTags);
        await tagFileService.WriteTagsAsync(focusedImage.TagFilePath, normalizedTags).ConfigureAwait(true);
        messenger.Send(new TagsChangedMessage(focusedImage.FilePath, normalizedTags));
        StatusText = string.Format(
            "Pasted {0} tag{1} onto {2}.",
            normalizedTags.Count,
            normalizedTags.Count == 1 ? string.Empty : "s",
            focusedImage.FileName);
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

    partial void OnZoomValueChanged(int value)
    {
        _ = value;

        if (currentProject is not null)
        {
            currentProject.State.ZoomSliderValue = ZoomValue;

            if (!isRestoringPersistedProjectState)
            {
                _ = PersistCurrentProjectStateAsync();
            }
        }
    }

    partial void OnItemsPerRowChanged(int value)
    {
        _ = value;
        RebuildImageRows();
    }

    partial void OnSelectedAiModelChanged(AiModelInfo? value)
    {
        if (currentProject is null)
        {
            return;
        }

        currentProject.AiModelName = value?.Id ?? string.Empty;
        currentProject.State.SelectedAiModelName = value?.Id;

        if (!isRestoringPersistedProjectState)
        {
            _ = PersistCurrentProjectStateAsync();
        }

        if (!string.IsNullOrWhiteSpace(value?.Id))
        {
            int queuedImageCount = QueueAiTaggingForImages(allImages);
            if (queuedImageCount > 0)
            {
                StatusText = string.Format(
                    "Queued AI tagging for {0} image{1} with {2}.",
                    queuedImageCount,
                    queuedImageCount == 1 ? string.Empty : "s",
                    value.DisplayName);
            }
        }
    }

    partial void OnBatchAddQueryTextChanged(string value)
    {
        if (IsBatchAddOpen)
        {
            _ = RefreshBatchAddSuggestionsAsync(value);
        }
    }

    partial void OnBatchRemoveQueryTextChanged(string value)
    {
        if (IsBatchRemoveOpen)
        {
            _ = RefreshBatchRemoveSuggestionsAsync(value);
        }
    }

    private void SetFocusedImage(LibraryGridImageViewModel? image, bool updateStatusText)
    {
        if (image is null)
        {
            ClearFocusState();
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

            if (!isRestoringPersistedProjectState)
            {
                _ = PersistCurrentProjectStateAsync();
            }
        }

        if (updateStatusText)
        {
            StatusText = string.Format("Focused {0}.", image.FileName);
        }
    }

    private void ClearFocusState()
    {
        foreach (LibraryGridImageViewModel image in Images)
        {
            image.IsFocused = false;
        }

        FocusedImageIndex = -1;
    }

    private async Task RestoreProjectStateAndLoadAsync(Project project)
    {
        isRestoringPersistedProjectState = true;

        try
        {
            ProjectState persistedState = await statePersistenceService.LoadProjectStateAsync(project.Id).ConfigureAwait(true);
            project.State = persistedState;

            if (!string.IsNullOrWhiteSpace(project.State.SelectedAiModelName))
            {
                project.AiModelName = project.State.SelectedAiModelName;
            }

            ZoomValue = project.State.ZoomSliderValue > 0 ? project.State.ZoomSliderValue : 160;
            await LoadAiModelChoicesAsync(project).ConfigureAwait(true);
            await LoadStagesCoreAsync().ConfigureAwait(true);
        }
        finally
        {
            isRestoringPersistedProjectState = false;
        }
    }

    private void ConfigureProjectWatcher()
    {
        DetachProjectWatcher();

        if (currentProject is null || string.IsNullOrWhiteSpace(currentProject.RootFolderPath) || !Directory.Exists(currentProject.RootFolderPath))
        {
            return;
        }

        FileSystemWatcher fileSystemWatcher = fileSystemService.WatchFolder(currentProject.RootFolderPath);
        fileSystemWatcher.Changed += OnProjectWatcherChanged;
        fileSystemWatcher.Created += OnProjectWatcherChanged;
        fileSystemWatcher.Deleted += OnProjectWatcherChanged;
        fileSystemWatcher.Renamed += OnProjectWatcherRenamed;
        fileSystemWatcher.EnableRaisingEvents = true;
        projectWatcher = fileSystemWatcher;
    }

    private void DetachProjectWatcher()
    {
        projectWatcherRefreshCancellationSource?.Cancel();
        projectWatcherRefreshCancellationSource?.Dispose();
        projectWatcherRefreshCancellationSource = null;

        lock (projectWatcherStateGate)
        {
            pendingThumbnailInvalidationPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        if (projectWatcher is null)
        {
            return;
        }

        projectWatcher.Changed -= OnProjectWatcherChanged;
        projectWatcher.Created -= OnProjectWatcherChanged;
        projectWatcher.Deleted -= OnProjectWatcherChanged;
        projectWatcher.Renamed -= OnProjectWatcherRenamed;
        projectWatcher.Dispose();
        projectWatcher = null;
    }

    private void OnProjectWatcherChanged(object? sender, FileSystemEventArgs eventArgs)
    {
        _ = sender;
        QueueProjectWatcherRefresh(eventArgs.FullPath);
    }

    private void OnProjectWatcherRenamed(object? sender, RenamedEventArgs eventArgs)
    {
        _ = sender;
        QueueProjectWatcherRefresh(eventArgs.OldFullPath, eventArgs.FullPath);
    }

    private void QueueProjectWatcherRefresh(params string?[] fullPaths)
    {
        if (currentProject is null || string.IsNullOrWhiteSpace(currentProject.RootFolderPath))
        {
            return;
        }

        bool shouldRefresh = false;

        lock (projectWatcherStateGate)
        {
            foreach (string? fullPath in fullPaths)
            {
                if (!ShouldReactToProjectChange(fullPath))
                {
                    continue;
                }

                shouldRefresh = true;

                if (!string.IsNullOrWhiteSpace(fullPath) && ImageFileTypeRules.IsSupportedImagePath(fullPath))
                {
                    pendingThumbnailInvalidationPaths.Add(fullPath);
                }
            }
        }

        if (!shouldRefresh)
        {
            return;
        }

        projectWatcherRefreshCancellationSource?.Cancel();
        projectWatcherRefreshCancellationSource?.Dispose();

        CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        projectWatcherRefreshCancellationSource = cancellationTokenSource;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(350, cancellationTokenSource.Token).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            await RefreshFromProjectWatcherAsync().ConfigureAwait(false);
        });
    }

    private bool ShouldReactToProjectChange(string? fullPath)
    {
        if (currentProject is null || string.IsNullOrWhiteSpace(fullPath))
        {
            return false;
        }

        string relativePath = Path.GetRelativePath(currentProject.RootFolderPath, fullPath);
        if (relativePath.StartsWith("..", StringComparison.Ordinal))
        {
            return false;
        }

        if (string.Equals(relativePath, ".datasetstudio.json", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (ImageFileTypeRules.IsSupportedImagePath(fullPath))
        {
            return true;
        }

        if (string.Equals(Path.GetExtension(fullPath), ".txt", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string[] pathSegments = relativePath
            .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);

        return pathSegments.Length > 0 && string.IsNullOrWhiteSpace(Path.GetExtension(fullPath));
    }

    private async Task RefreshFromProjectWatcherAsync()
    {
        HashSet<string> thumbnailPathsToInvalidate;

        lock (projectWatcherStateGate)
        {
            thumbnailPathsToInvalidate = pendingThumbnailInvalidationPaths;
            pendingThumbnailInvalidationPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        foreach (string thumbnailPath in thumbnailPathsToInvalidate)
        {
            await thumbnailCacheService.InvalidateAsync(thumbnailPath).ConfigureAwait(true);
        }

        if (currentProject is null)
        {
            return;
        }

        await LoadStagesCoreAsync().ConfigureAwait(true);
        StatusText = string.Format("Detected on-disk changes in {0}.", ProjectName);
    }

    private Task PersistCurrentProjectStateAsync()
    {
        if (currentProject is null || string.IsNullOrWhiteSpace(currentProject.Id))
        {
            return Task.CompletedTask;
        }

        return PersistCurrentProjectStateCoreAsync(currentProject.Id, currentProject.State);
    }

    private async Task PersistCurrentProjectStateCoreAsync(string projectId, ProjectState state)
    {
        try
        {
            await statePersistenceService.SaveProjectStateAsync(projectId, state).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            StatusText = string.Format("Could not save project state: {0}", exception.Message);
        }
    }

    private void ApplyImageFilter()
    {
        string? preferredFocusedImagePath = GetFocusedImage()?.FilePath;
        int previousFocusedImageIndex = FocusedImageIndex;
        IEnumerable<LibraryGridImageViewModel> filteredImages = allImages;

        if (!string.IsNullOrWhiteSpace(FilterText))
        {
            filteredImages = filteredImages.Where(image =>
                image.FileName.Contains(FilterText, StringComparison.OrdinalIgnoreCase)
                || image.TagsPreview.Contains(FilterText, StringComparison.OrdinalIgnoreCase));
        }

        Images = new ObservableCollection<LibraryGridImageViewModel>(filteredImages);
        HasImages = Images.Count > 0;
        RebuildImageRows();

        if (Images.Count == 0)
        {
            ClearFocusState();
            return;
        }

        LibraryGridImageViewModel? preferredImage = preferredFocusedImagePath is null
            ? null
            : Images.FirstOrDefault(image =>
                string.Equals(image.FilePath, preferredFocusedImagePath, StringComparison.OrdinalIgnoreCase));

        if (preferredImage is not null)
        {
            SetFocusedImage(preferredImage, updateStatusText: false);
            return;
        }

        if (currentProject is not null && !string.IsNullOrWhiteSpace(currentProject.State.LastInspectedImagePath))
        {
            LibraryGridImageViewModel? lastInspectedImage = Images.FirstOrDefault(image =>
                string.Equals(image.FilePath, currentProject.State.LastInspectedImagePath, StringComparison.OrdinalIgnoreCase));

            if (lastInspectedImage is not null)
            {
                SetFocusedImage(lastInspectedImage, updateStatusText: false);
                return;
            }
        }

        int clampedIndex = previousFocusedImageIndex;
        if (clampedIndex < 0)
        {
            clampedIndex = 0;
        }

        if (clampedIndex >= Images.Count)
        {
            clampedIndex = Images.Count - 1;
        }

        SetFocusedImage(Images[clampedIndex], updateStatusText: false);
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
    }

    private void RebuildImageRows()
    {
        int effectiveItemsPerRow = ItemsPerRow;
        if (effectiveItemsPerRow < 1)
        {
            effectiveItemsPerRow = 1;
        }

        List<LibraryGridImageRowViewModel> rows = new List<LibraryGridImageRowViewModel>();
        for (int index = 0; index < Images.Count; index += effectiveItemsPerRow)
        {
            IEnumerable<LibraryGridImageViewModel> rowImages = Images.Skip(index).Take(effectiveItemsPerRow);
            rows.Add(new LibraryGridImageRowViewModel(rowImages));
        }

        ImageRows = new ObservableCollection<LibraryGridImageRowViewModel>(rows);
    }

    private async Task LoadAiModelChoicesAsync(Project project)
    {
        IReadOnlyList<AiModelInfo> availableModels;

        try
        {
            availableModels = await aiTaggerService.GetAvailableModelsAsync().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            availableModels = Array.Empty<AiModelInfo>();
            StatusText = string.Format("Could not load AI models: {0}", exception.Message);
        }

        AiModels.Clear();

        foreach (AiModelInfo availableModel in availableModels)
        {
            AiModels.Add(availableModel);
        }

        string? selectedModelName = AiTaggingModelResolver.ResolveConfiguredModelName(project);
        if (!string.IsNullOrWhiteSpace(selectedModelName))
        {
            AiModelInfo? selectedModel = AiModels.FirstOrDefault(model =>
                string.Equals(model.Id, selectedModelName, StringComparison.OrdinalIgnoreCase));

            if (selectedModel is null)
            {
                selectedModel = new AiModelInfo
                {
                    Id = selectedModelName,
                    DisplayName = selectedModelName,
                    ModelPath = string.Empty,
                };

                AiModels.Insert(0, selectedModel);
            }

            SelectedAiModel = selectedModel;
        }
        else
        {
            SelectedAiModel = null;
        }
    }

    private int QueueAiTaggingForImages(IEnumerable<LibraryGridImageViewModel> images)
    {
        int queuedImageCount = 0;

        foreach (LibraryGridImageViewModel image in images)
        {
            if (QueueAiTaggingIfNeeded(image))
            {
                queuedImageCount++;
            }
        }

        return queuedImageCount;
    }

    private bool QueueAiTaggingIfNeeded(LibraryGridImageViewModel image)
    {
        if (currentProject is null)
        {
            return false;
        }

        if (tagFileService.TagFileExists(image.FilePath))
        {
            image.IsAiProcessing = aiTaggerService.IsProcessing(image.FilePath);
            return false;
        }

        string? aiModelName = AiTaggingModelResolver.ResolveConfiguredModelName(currentProject);
        if (string.IsNullOrWhiteSpace(aiModelName))
        {
            image.IsAiProcessing = false;
            return false;
        }

        if (aiTaggerService.IsProcessing(image.FilePath))
        {
            image.IsAiProcessing = true;
            return false;
        }

        bool wasQueued = aiTaggerService.TryQueueTagGeneration(currentProject, image.FilePath);
        image.IsAiProcessing = wasQueued || aiTaggerService.IsProcessing(image.FilePath);
        return wasQueued;
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
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return combinedTags;
    }

    private HashSet<string> BuildNormalizedPrefixTagSet()
    {
        if (currentProject is null)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        HashSet<string> prefixTagSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string prefixTag in currentProject.PrefixTags)
        {
            if (string.IsNullOrWhiteSpace(prefixTag))
            {
                continue;
            }

            string trimmedPrefixTag = prefixTag.Trim();
            prefixTagSet.Add(trimmedPrefixTag);
            prefixTagSet.Add(tagDictionaryService.ResolveAlias(currentProject.Id, trimmedPrefixTag));
        }

        return prefixTagSet;
    }

    private List<string> NormalizeClipboardTags(IReadOnlyList<string> sourceTags)
    {
        List<string> normalizedTags = new List<string>();
        HashSet<string> prefixTagSet = BuildNormalizedPrefixTagSet();

        foreach (string sourceTag in sourceTags)
        {
            string trimmedTag = sourceTag.Trim();
            if (string.IsNullOrWhiteSpace(trimmedTag))
            {
                continue;
            }

            string resolvedTag = currentProject is null
                ? trimmedTag
                : tagDictionaryService.ResolveAlias(currentProject.Id, trimmedTag);

            if (prefixTagSet.Contains(trimmedTag) || prefixTagSet.Contains(resolvedTag))
            {
                continue;
            }

            if (normalizedTags.Any(existingTag => string.Equals(existingTag, resolvedTag, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            normalizedTags.Add(resolvedTag);
        }

        return normalizedTags;
    }

    private Task RefreshBatchAddSuggestionsAsync(string query)
    {
        return RefreshBatchAddSuggestionsCoreAsync(query);
    }

    private Task RefreshBatchRemoveSuggestionsAsync(string query)
    {
        return RefreshBatchRemoveSuggestionsCoreAsync(query);
    }

    private Task<IReadOnlyList<BatchTagSuggestionViewModel>> BuildRemoveSuggestionsAsync(string query)
    {
        return BuildRemoveSuggestionsCoreAsync(query);
    }

    private static string GetBatchTagInput(BatchTagSuggestionViewModel? selectedSuggestion, string queryText)
    {
        string trimmedQuery = queryText.Trim();
        if (!string.IsNullOrWhiteSpace(trimmedQuery))
        {
            if (selectedSuggestion is null
                || string.IsNullOrWhiteSpace(selectedSuggestion.Tag)
                || !string.Equals(selectedSuggestion.Tag, trimmedQuery, StringComparison.OrdinalIgnoreCase))
            {
                return trimmedQuery;
            }
        }

        if (selectedSuggestion is not null && !string.IsNullOrWhiteSpace(selectedSuggestion.Tag))
        {
            return selectedSuggestion.Tag;
        }

        return trimmedQuery;
    }

    private IReadOnlyList<LibraryGridImageViewModel> GetBatchScopeImages()
    {
        if (SelectedImages.Count > 0)
        {
            return SelectedImages.ToList();
        }

        return allImages.ToList();
    }

    private IReadOnlyList<LibraryGridImageViewModel> GetStrictSelectedImages()
    {
        return SelectedImages.ToList();
    }

    private LibraryGridImageViewModel? GetFocusedImage()
    {
        if (FocusedImageIndex < 0 || FocusedImageIndex >= Images.Count)
        {
            return null;
        }

        return Images[FocusedImageIndex];
    }

    private static string GetMovedImagePath(string sourceImagePath, string destinationFolder)
    {
        return Path.Combine(destinationFolder, Path.GetFileName(sourceImagePath));
    }

    private void UpdateLastInspectedImagePathForMovedImage(LibraryGridImageViewModel image, string destinationFolder)
    {
        if (currentProject is null)
        {
            return;
        }

        if (!string.Equals(currentProject.State.LastInspectedImagePath, image.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        currentProject.State.LastInspectedImagePath = GetMovedImagePath(image.FilePath, destinationFolder);
    }

    private Task<Bitmap?> LoadThumbnailAsync(string imageFilePath)
    {
        return LoadThumbnailCoreAsync(imageFilePath);
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

        if (Images.Count == 0 && currentProject is not null)
        {
            currentProject.State.LastInspectedImagePath = null;
        }
    }

    private void HandleImageMoved(ImageMovedMessage message)
    {
        if (isIgnoringInternalImageMovedMessages || ActiveStage is null)
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

    private void HandleAiTaggingFailed(AiTaggingFailedMessage message)
    {
        LibraryGridImageViewModel? image = allImages.FirstOrDefault(candidate =>
            string.Equals(candidate.FilePath, message.ImagePath, StringComparison.OrdinalIgnoreCase));

        if (image is null)
        {
            return;
        }

        image.IsAiProcessing = false;
        image.Status = TagStatus.Untagged;
        StatusText = string.Format("AI tagging failed for {0}: {1}", image.FileName, message.ErrorMessage);
    }

    private async Task RefreshBatchAddSuggestionsCoreAsync(string query)
    {
        if (currentProject is null)
        {
            BatchAddSuggestions = new ObservableCollection<BatchTagSuggestionViewModel>();
            SelectedBatchAddSuggestion = null;
            return;
        }

        string trimmedQuery = query.Trim();
        IReadOnlyList<TagDictionaryEntry> entries = await tagDictionaryService.GetAllEntriesAsync(currentProject.Id).ConfigureAwait(true);
        if (!string.Equals(BatchAddQueryText.Trim(), trimmedQuery, StringComparison.Ordinal))
        {
            return;
        }

        IEnumerable<TagDictionaryEntry> filteredEntries = entries;
        if (!string.IsNullOrWhiteSpace(trimmedQuery))
        {
            filteredEntries = filteredEntries.Where(entry =>
                entry.CanonicalName.Contains(trimmedQuery, StringComparison.OrdinalIgnoreCase)
                || entry.Aliases.Any(alias => alias.Contains(trimmedQuery, StringComparison.OrdinalIgnoreCase)));
        }

        List<BatchTagSuggestionViewModel> suggestions = filteredEntries
            .OrderByDescending(entry => entry.GlobalFrequency)
            .ThenBy(entry => entry.CanonicalName, StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .Select(entry => new BatchTagSuggestionViewModel(entry.CanonicalName, entry.GlobalFrequency))
            .ToList();

        if (!string.IsNullOrWhiteSpace(trimmedQuery)
            && !suggestions.Any(suggestion => string.Equals(suggestion.Tag, trimmedQuery, StringComparison.OrdinalIgnoreCase)))
        {
            suggestions.Insert(0, new BatchTagSuggestionViewModel(trimmedQuery, 0));
        }

        BatchAddSuggestions = new ObservableCollection<BatchTagSuggestionViewModel>(suggestions);
        SelectedBatchAddSuggestion = BatchAddSuggestions.FirstOrDefault();
    }

    private async Task RefreshBatchRemoveSuggestionsCoreAsync(string query)
    {
        if (currentProject is null)
        {
            BatchRemoveSuggestions = new ObservableCollection<BatchTagSuggestionViewModel>();
            SelectedBatchRemoveSuggestion = null;
            return;
        }

        string trimmedQuery = query.Trim();
        IReadOnlyList<BatchTagSuggestionViewModel> suggestions = await BuildRemoveSuggestionsCoreAsync(query).ConfigureAwait(true);
        if (!string.Equals(BatchRemoveQueryText.Trim(), trimmedQuery, StringComparison.Ordinal))
        {
            return;
        }

        BatchRemoveSuggestions = new ObservableCollection<BatchTagSuggestionViewModel>(suggestions);
        SelectedBatchRemoveSuggestion = BatchRemoveSuggestions.FirstOrDefault();
    }

    private async Task<IReadOnlyList<BatchTagSuggestionViewModel>> BuildRemoveSuggestionsCoreAsync(string query)
    {
        if (currentProject is null)
        {
            return Array.Empty<BatchTagSuggestionViewModel>();
        }

        Dictionary<string, int> frequencyByTag = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (LibraryGridImageViewModel image in allImages)
        {
            IReadOnlyList<string> fileTags = await tagFileService.ReadTagsAsync(image.TagFilePath).ConfigureAwait(true);
            HashSet<string> normalizedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string fileTag in fileTags)
            {
                string trimmedTag = fileTag.Trim();
                if (string.IsNullOrWhiteSpace(trimmedTag))
                {
                    continue;
                }

                string resolvedTag = tagDictionaryService.ResolveAlias(currentProject.Id, trimmedTag);
                if (!string.IsNullOrWhiteSpace(resolvedTag))
                {
                    normalizedTags.Add(resolvedTag);
                }
            }

            foreach (string resolvedTag in normalizedTags)
            {
                if (frequencyByTag.TryGetValue(resolvedTag, out int existingFrequency))
                {
                    frequencyByTag[resolvedTag] = existingFrequency + 1;
                }
                else
                {
                    frequencyByTag[resolvedTag] = 1;
                }
            }
        }

        IEnumerable<KeyValuePair<string, int>> filteredEntries = frequencyByTag;
        string trimmedQuery = query.Trim();
        if (!string.IsNullOrWhiteSpace(trimmedQuery))
        {
            filteredEntries = filteredEntries.Where(entry =>
                entry.Key.Contains(trimmedQuery, StringComparison.OrdinalIgnoreCase));
        }

        return filteredEntries
            .OrderByDescending(entry => entry.Value)
            .ThenBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .Select(entry => new BatchTagSuggestionViewModel(entry.Key, entry.Value))
            .ToList();
    }

    private async Task<Bitmap?> LoadThumbnailCoreAsync(string imageFilePath)
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
}
