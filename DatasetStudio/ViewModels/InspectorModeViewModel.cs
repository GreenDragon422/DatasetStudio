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

public partial class InspectorModeViewModel : ScreenViewModelBase, INavigationAware
{
    private readonly ITagFileService tagFileService;
    private readonly ITagDictionaryService tagDictionaryService;
    private readonly IFileSystemService fileSystemService;
    private readonly IClipboardService clipboardService;
    private readonly INavigationService navigationService;
    private readonly IAiTaggerService aiTaggerService;
    private readonly IMessenger messenger;
    private readonly IStatePersistenceService statePersistenceService;

    private Project? currentProject;
    private FileSystemWatcher? projectWatcher;
    private CancellationTokenSource? projectWatcherRefreshCancellationSource;
    private bool isSynchronizingStageSelection;
    private bool isIgnoringNextImageMovedMessage;
    private bool isRestoringPersistedProjectState;

    public InspectorModeViewModel(
        ITagFileService tagFileService,
        ITagDictionaryService tagDictionaryService,
        IFileSystemService fileSystemService,
        IClipboardService clipboardService,
        INavigationService navigationService,
        IAiTaggerService aiTaggerService,
        IMessenger messenger,
        IStatePersistenceService statePersistenceService)
        : base(messenger)
    {
        this.tagFileService = tagFileService ?? throw new ArgumentNullException(nameof(tagFileService));
        this.tagDictionaryService = tagDictionaryService ?? throw new ArgumentNullException(nameof(tagDictionaryService));
        this.fileSystemService = fileSystemService ?? throw new ArgumentNullException(nameof(fileSystemService));
        this.clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
        this.navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        this.aiTaggerService = aiTaggerService ?? throw new ArgumentNullException(nameof(aiTaggerService));
        this.messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        this.statePersistenceService = statePersistenceService ?? throw new ArgumentNullException(nameof(statePersistenceService));

        Stages = new ObservableCollection<LibraryGridStageViewModel>();
        ImageList = new ObservableCollection<ImageEntry>();
        AppliedTags = new ObservableCollection<string>();
        AutoSuggestTags = new ObservableCollection<string>();
        StatusText = "Open an image from Review Workspace to inspect it.";

        messenger.Register<InspectorModeViewModel, AiTaggingCompletedMessage>(this, static (recipient, message) =>
        {
            _ = recipient.HandleAiTaggingCompletedAsync(message);
        });
        messenger.Register<InspectorModeViewModel, AiTaggingFailedMessage>(this, static (recipient, message) =>
        {
            recipient.HandleAiTaggingFailed(message);
        });
        messenger.Register<InspectorModeViewModel, ImageMovedMessage>(this, static (recipient, message) =>
        {
            _ = recipient.HandleImageMovedAsync(message);
        });
    }

    [ObservableProperty]
    private ObservableCollection<LibraryGridStageViewModel> stages;

    [ObservableProperty]
    private LibraryGridStageViewModel? activeStage;

    [ObservableProperty]
    private ObservableCollection<ImageEntry> imageList;

    [ObservableProperty]
    private ImageEntry? currentImage;

    [ObservableProperty]
    private Bitmap? currentImageSource;

    [ObservableProperty]
    private string projectName = string.Empty;

    [ObservableProperty]
    private string fileName = string.Empty;

    [ObservableProperty]
    private TagStatus currentStatus;

    [ObservableProperty]
    private string prefixTagsText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> prefixTags = new ObservableCollection<string>();

    [ObservableProperty]
    private ObservableCollection<string> appliedTags;

    [ObservableProperty]
    private string tagInputText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> autoSuggestTags;

    [ObservableProperty]
    private bool isSuggestOpen;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private int currentIndex = -1;

    public void OnNavigatedTo(object parameter)
    {
        if (parameter is not Project project)
        {
            StatusText = "Inspector Mode requires a project navigation parameter.";
            return;
        }

        currentProject = project;
        ProjectName = string.IsNullOrWhiteSpace(project.Name) ? Path.GetFileName(project.RootFolderPath) : project.Name;
        PrefixTags = new ObservableCollection<string>(project.PrefixTags);
        PrefixTagsText = project.PrefixTags.Count == 0
            ? "No prefix tags configured."
            : string.Join(", ", project.PrefixTags);
        StatusText = string.Format("Loading Inspector Mode for {0}...", ProjectName);
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

    partial void OnActiveStageChanged(LibraryGridStageViewModel? value)
    {
        if (value is null || isSynchronizingStageSelection)
        {
            return;
        }

        _ = LoadStageImagesAsync(value, currentProject?.State.LastInspectedImagePath, null);
    }

    partial void OnTagInputTextChanged(string value)
    {
        _ = RefreshSuggestionsAsync(value);
    }

    [RelayCommand]
    private async Task LoadImageAsync(ImageEntry? image)
    {
        if (image is null)
        {
            return;
        }

        int imageIndex = FindImageIndex(image.FilePath);
        if (imageIndex < 0)
        {
            return;
        }

        await NavigateToImageIndexAsync(imageIndex).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task CommitTagAsync()
    {
        if (currentProject is null || CurrentImage is null)
        {
            return;
        }

        string normalizedInput = TagInputText.Trim();
        if (string.IsNullOrWhiteSpace(normalizedInput))
        {
            return;
        }

        string resolvedTag = tagDictionaryService.ResolveAlias(currentProject.Id, normalizedInput);
        if (string.IsNullOrWhiteSpace(resolvedTag))
        {
            return;
        }

        if (!AppliedTags.Any(existingTag => string.Equals(existingTag, resolvedTag, StringComparison.OrdinalIgnoreCase)))
        {
            AppliedTags.Add(resolvedTag);
        }

        await PersistCurrentTagsAsync().ConfigureAwait(true);
        TagInputText = string.Empty;
        await NavigateToNextPendingImageAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RemoveTagAsync(string? tag)
    {
        if (CurrentImage is null || string.IsNullOrWhiteSpace(tag))
        {
            return;
        }

        string removedTag = tag.Trim();
        string? existingTag = AppliedTags.FirstOrDefault(candidate =>
            string.Equals(candidate, removedTag, StringComparison.OrdinalIgnoreCase));
        if (existingTag is null)
        {
            return;
        }

        AppliedTags.Remove(existingTag);
        await PersistCurrentTagsAsync().ConfigureAwait(true);
        StatusText = string.Format("Removed {0} from {1}.", removedTag, CurrentImage.FileName);
    }

    [RelayCommand]
    private async Task NavigatePreviousAsync()
    {
        await NavigateImageAsync(-1).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task NavigateNextAsync()
    {
        await NavigateImageAsync(1).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task NavigateImageAsync(int offset)
    {
        await NavigateToImageIndexAsync(CurrentIndex + offset).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task MoveToPreviousStageAsync()
    {
        await MoveImageAsync(-1).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task MoveToNextStageAsync()
    {
        await MoveImageAsync(1).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task MoveImageAsync(int stageOffset)
    {
        await MoveCurrentImageAsync(stageOffset).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task DeleteImageAsync()
    {
        if (CurrentImage is null || ActiveStage is null)
        {
            return;
        }

        ImageEntry deletedImage = CurrentImage;
        int preferredIndex = CurrentIndex;
        await fileSystemService.RecycleFileAsync(deletedImage.FilePath).ConfigureAwait(true);

        if (tagFileService.TagFileExists(deletedImage.FilePath))
        {
            await fileSystemService.RecycleFileAsync(deletedImage.TagFilePath).ConfigureAwait(true);
        }

        if (currentProject is not null)
        {
            currentProject.State.LastInspectedImagePath = null;
            await PersistCurrentProjectStateAsync().ConfigureAwait(true);
        }

        messenger.Send(new ImageDeletedMessage(deletedImage.FilePath, ActiveStage.FolderPath));
        StatusText = string.Format("Recycled {0}.", deletedImage.FileName);
        await LoadStagesAsync(ActiveStage.FolderName, null, preferredIndex).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task CopyTagsAsync()
    {
        if (currentProject is null)
        {
            return;
        }

        List<string> fullTagSet = currentProject.PrefixTags
            .Concat(AppliedTags)
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        await clipboardService.CopyTagsAsync(fullTagSet).ConfigureAwait(true);
        StatusText = "Copied tags to the clipboard.";
    }

    [RelayCommand]
    private async Task PasteTagsAsync()
    {
        if (currentProject is null || CurrentImage is null)
        {
            return;
        }

        IReadOnlyList<string> clipboardTags = await clipboardService.PasteTagsAsync().ConfigureAwait(true);
        if (clipboardTags.Count == 0)
        {
            StatusText = "Clipboard does not contain any tags.";
            return;
        }

        List<string> normalizedTags = NormalizeAppliedTags(clipboardTags);
        AppliedTags = new ObservableCollection<string>(normalizedTags);
        await PersistCurrentTagsAsync().ConfigureAwait(true);
        StatusText = string.Format("Pasted {0} tag{1} onto {2}.", normalizedTags.Count, normalizedTags.Count == 1 ? string.Empty : "s", CurrentImage.FileName);
    }

    [RelayCommand]
    private async Task UseSuggestionAsync(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return;
        }

        TagInputText = tag;
        await CommitTagAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private void GoBack()
    {
        navigationService.GoBack();
    }

    private async Task LoadStagesAsync(string? preferredStageFolderName, string? preferredImagePath, int? preferredIndex)
    {
        if (currentProject is null)
        {
            return;
        }

        _ = await tagDictionaryService.GetAllEntriesAsync(currentProject.Id).ConfigureAwait(true);

        IReadOnlyList<WorkflowStage> workflowStages = GetWorkflowStages(currentProject);
        List<LibraryGridStageViewModel> stageViewModels = new List<LibraryGridStageViewModel>();

        foreach (WorkflowStage workflowStage in workflowStages)
        {
            string folderPath = GetStageFolderPath(currentProject, workflowStage);
            IReadOnlyList<string> imageFiles = await fileSystemService.GetImageFilesAsync(folderPath).ConfigureAwait(true);
            stageViewModels.Add(new LibraryGridStageViewModel(workflowStage, folderPath, imageFiles.Count));
        }

        Stages = new ObservableCollection<LibraryGridStageViewModel>(stageViewModels);

        LibraryGridStageViewModel? selectedStage = stageViewModels.FirstOrDefault(stage =>
            string.Equals(stage.FolderName, preferredStageFolderName, StringComparison.OrdinalIgnoreCase))
            ?? stageViewModels.FirstOrDefault(stage =>
                string.Equals(stage.FolderName, currentProject.State.ActiveStageFolderName, StringComparison.OrdinalIgnoreCase))
            ?? stageViewModels.FirstOrDefault();

        isSynchronizingStageSelection = true;
        ActiveStage = selectedStage;
        isSynchronizingStageSelection = false;

        if (selectedStage is null)
        {
            ReplaceImageList(Array.Empty<ImageEntry>());
            StatusText = "No workflow stages are available for Inspector Mode.";
            return;
        }

        await LoadStageImagesAsync(selectedStage, preferredImagePath, preferredIndex).ConfigureAwait(true);
    }

    private async Task LoadStageImagesAsync(LibraryGridStageViewModel stage, string? preferredImagePath, int? preferredIndex)
    {
        if (currentProject is null)
        {
            return;
        }

        currentProject.State.ActiveStageFolderName = stage.FolderName;
        if (!isRestoringPersistedProjectState)
        {
            _ = PersistCurrentProjectStateAsync();
        }

        IReadOnlyList<string> imageFilePaths = await fileSystemService.GetImageFilesAsync(stage.FolderPath).ConfigureAwait(true);
        List<ImageEntry> imageEntries = new List<ImageEntry>();

        foreach (string imageFilePath in imageFilePaths)
        {
            string tagFilePath = tagFileService.GetTagFilePath(imageFilePath);
            IReadOnlyList<string> fileTags = await tagFileService.ReadTagsAsync(tagFilePath).ConfigureAwait(true);
            TagStatus imageStatus = fileTags.Count == 0 ? TagStatus.Untagged : TagStatus.Ready;

            ImageEntry imageEntry = new ImageEntry
            {
                FilePath = imageFilePath,
                FileName = Path.GetFileName(imageFilePath),
                TagFilePath = tagFilePath,
                Status = imageStatus,
                Tags = fileTags.ToList(),
                IsAiProcessing = aiTaggerService.IsProcessing(imageFilePath),
            };

            imageEntries.Add(imageEntry);
        }

        ReplaceImageList(imageEntries);
        int queuedImageCount = QueueAiTaggingForImages(imageEntries);

        if (ImageList.Count == 0)
        {
            DisposeCurrentBitmap();
            CurrentImageSource = null;
            CurrentImage = null;
            FileName = string.Empty;
            CurrentStatus = TagStatus.Untagged;
            AppliedTags = new ObservableCollection<string>();
            AutoSuggestTags = new ObservableCollection<string>();
            IsSuggestOpen = false;
            TagInputText = string.Empty;
            StatusText = string.Format("{0} is empty.", stage.DisplayName);
            return;
        }

        int nextIndex = ResolvePreferredIndex(preferredImagePath, preferredIndex);
        await NavigateToImageIndexAsync(nextIndex).ConfigureAwait(true);

        if (queuedImageCount > 0)
        {
            StatusText = string.Format(
                "{0} Queued AI tagging for {1} image{2}.",
                StatusText,
                queuedImageCount,
                queuedImageCount == 1 ? string.Empty : "s");
        }
    }

    private int ResolvePreferredIndex(string? preferredImagePath, int? preferredIndex)
    {
        if (!string.IsNullOrWhiteSpace(preferredImagePath))
        {
            int preferredImageIndex = FindImageIndex(preferredImagePath);
            if (preferredImageIndex >= 0)
            {
                return preferredImageIndex;
            }
        }

        if (!string.IsNullOrWhiteSpace(currentProject?.State.LastInspectedImagePath))
        {
            int stateImageIndex = FindImageIndex(currentProject.State.LastInspectedImagePath);
            if (stateImageIndex >= 0)
            {
                return stateImageIndex;
            }
        }

        if (preferredIndex.HasValue)
        {
            int safeIndex = Math.Clamp(preferredIndex.Value, 0, ImageList.Count - 1);
            return safeIndex;
        }

        int? nextPendingIndex = FindNextPendingImageIndex(-1);
        if (nextPendingIndex.HasValue)
        {
            return nextPendingIndex.Value;
        }

        return 0;
    }

    private int FindImageIndex(string imagePath)
    {
        for (int index = 0; index < ImageList.Count; index++)
        {
            if (string.Equals(ImageList[index].FilePath, imagePath, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private async Task NavigateToImageIndexAsync(int targetIndex)
    {
        if (targetIndex < 0 || targetIndex >= ImageList.Count)
        {
            return;
        }

        ImageEntry image = ImageList[targetIndex];
        CurrentIndex = targetIndex;
        CurrentImage = image;
        FileName = image.FileName;
        CurrentStatus = image.Status;
        IsBusy = image.IsAiProcessing;

        if (currentProject is not null)
        {
            currentProject.State.LastInspectedImagePath = image.FilePath;

            if (!isRestoringPersistedProjectState)
            {
                _ = PersistCurrentProjectStateAsync();
            }
        }

        IReadOnlyList<string> fileTags = await tagFileService.ReadTagsAsync(image.TagFilePath).ConfigureAwait(true);
        AppliedTags = new ObservableCollection<string>(fileTags);
        AutoSuggestTags.Clear();
        IsSuggestOpen = false;

        Bitmap? loadedBitmap = await LoadBitmapAsync(image.FilePath).ConfigureAwait(true);
        DisposeCurrentBitmap();
        CurrentImageSource = loadedBitmap;

        StatusText = string.Format("Inspecting {0} ({1}/{2}).", image.FileName, targetIndex + 1, ImageList.Count);
    }

    private async Task RestoreProjectStateAndLoadAsync(Project project)
    {
        isRestoringPersistedProjectState = true;

        try
        {
            ProjectState persistedState = await statePersistenceService.LoadProjectStateAsync(project.Id).ConfigureAwait(true);
            project.State = persistedState;
            await LoadStagesAsync(project.State.ActiveStageFolderName, project.State.LastInspectedImagePath, null).ConfigureAwait(true);
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

        if (!ShouldReactToProjectChange(eventArgs.FullPath))
        {
            return;
        }

        QueueProjectWatcherRefresh();
    }

    private void OnProjectWatcherRenamed(object? sender, RenamedEventArgs eventArgs)
    {
        _ = sender;

        if (!ShouldReactToProjectChange(eventArgs.OldFullPath) && !ShouldReactToProjectChange(eventArgs.FullPath))
        {
            return;
        }

        QueueProjectWatcherRefresh();
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

    private void QueueProjectWatcherRefresh()
    {
        if (currentProject is null || string.IsNullOrWhiteSpace(currentProject.RootFolderPath))
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

    private async Task RefreshFromProjectWatcherAsync()
    {
        if (currentProject is null)
        {
            return;
        }

        string? preferredStageFolderName = ActiveStage?.FolderName ?? currentProject.State.ActiveStageFolderName;
        string? preferredImagePath = CurrentImage?.FilePath ?? currentProject.State.LastInspectedImagePath;
        int? preferredIndex = CurrentIndex >= 0 ? CurrentIndex : null;

        await LoadStagesAsync(preferredStageFolderName, preferredImagePath, preferredIndex).ConfigureAwait(true);
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

    private async Task<Bitmap?> LoadBitmapAsync(string imageFilePath)
    {
        try
        {
            return await Task.Run(() =>
            {
                using FileStream imageStream = File.OpenRead(imageFilePath);
                return new Bitmap(imageStream);
            }).ConfigureAwait(true);
        }
        catch
        {
            return null;
        }
    }

    private async Task PersistCurrentTagsAsync()
    {
        if (CurrentImage is null)
        {
            return;
        }

        List<string> normalizedTags = NormalizeAppliedTags(AppliedTags);
        await tagFileService.WriteTagsAsync(CurrentImage.TagFilePath, normalizedTags).ConfigureAwait(true);

        CurrentImage.Tags = normalizedTags;
        CurrentImage.Status = normalizedTags.Count == 0 ? TagStatus.Untagged : TagStatus.Ready;
        CurrentStatus = CurrentImage.Status;
        messenger.Send(new TagsChangedMessage(CurrentImage.FilePath, normalizedTags));
    }

    private List<string> NormalizeAppliedTags(IEnumerable<string> sourceTags)
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

            if (currentProject is not null && currentProject.PrefixTags.Any(prefixTag =>
                string.Equals(prefixTag, trimmedTag, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            string resolvedTag = currentProject is null
                ? trimmedTag
                : tagDictionaryService.ResolveAlias(currentProject.Id, trimmedTag);

            if (prefixTagSet.Contains(resolvedTag))
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

    private async Task RefreshSuggestionsAsync(string queryText)
    {
        if (currentProject is null)
        {
            return;
        }

        string normalizedQuery = queryText.Trim();
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            AutoSuggestTags.Clear();
            IsSuggestOpen = false;
            return;
        }

        IReadOnlyList<string> suggestedTags = await tagDictionaryService.SearchTagsAsync(currentProject.Id, normalizedQuery).ConfigureAwait(true);
        if (!string.Equals(TagInputText.Trim(), normalizedQuery, StringComparison.Ordinal))
        {
            return;
        }

        List<string> filteredSuggestions = suggestedTags
            .Where(suggestedTag => !AppliedTags.Any(appliedTag =>
                string.Equals(appliedTag, suggestedTag, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        AutoSuggestTags = new ObservableCollection<string>(filteredSuggestions);
        IsSuggestOpen = filteredSuggestions.Count > 0;
    }

    private async Task NavigateToNextPendingImageAsync()
    {
        if (ImageList.Count == 0)
        {
            return;
        }

        int? nextPendingIndex = FindNextPendingImageIndex(CurrentIndex);
        if (!nextPendingIndex.HasValue)
        {
            if (CurrentImage is not null)
            {
                StatusText = string.Format("Saved {0}. No more pending images remain in this stage.", CurrentImage.FileName);
            }

            return;
        }

        await NavigateToImageIndexAsync(nextPendingIndex.Value).ConfigureAwait(true);
    }

    private int? FindNextPendingImageIndex(int startIndex)
    {
        if (ImageList.Count <= 1)
        {
            return null;
        }

        for (int offset = 1; offset < ImageList.Count; offset++)
        {
            int candidateIndex = (startIndex + offset + ImageList.Count) % ImageList.Count;
            ImageEntry candidate = ImageList[candidateIndex];
            if (candidate.Status != TagStatus.Ready)
            {
                return candidateIndex;
            }
        }

        return null;
    }

    private async Task MoveCurrentImageAsync(int stageOffset)
    {
        if (CurrentImage is null || ActiveStage is null)
        {
            return;
        }

        int currentStageIndex = Stages.IndexOf(ActiveStage);
        int targetStageIndex = currentStageIndex + stageOffset;
        if (targetStageIndex < 0 || targetStageIndex >= Stages.Count)
        {
            return;
        }

        LibraryGridStageViewModel targetStage = Stages[targetStageIndex];
        ImageEntry movedImage = CurrentImage;
        string sourceFolder = ActiveStage.FolderPath;
        int preferredIndex = CurrentIndex;

        await fileSystemService.MoveFileAsync(movedImage.FilePath, targetStage.FolderPath).ConfigureAwait(true);
        if (tagFileService.TagFileExists(movedImage.FilePath))
        {
            await fileSystemService.MoveFileAsync(movedImage.TagFilePath, targetStage.FolderPath).ConfigureAwait(true);
        }

        string targetImagePath = Path.Combine(targetStage.FolderPath, movedImage.FileName);
        isIgnoringNextImageMovedMessage = true;
        messenger.Send(new ImageMovedMessage(targetImagePath, sourceFolder, targetStage.FolderPath));
        isIgnoringNextImageMovedMessage = false;

        currentProject!.State.LastInspectedImagePath = null;
        await PersistCurrentProjectStateAsync().ConfigureAwait(true);
        StatusText = string.Format("Moved {0} to {1}.", movedImage.FileName, targetStage.DisplayName);
        await LoadStagesAsync(ActiveStage.FolderName, null, preferredIndex).ConfigureAwait(true);
    }

    private async Task HandleAiTaggingCompletedAsync(AiTaggingCompletedMessage message)
    {
        ImageEntry? image = ImageList.FirstOrDefault(candidate =>
            string.Equals(candidate.FilePath, message.ImagePath, StringComparison.OrdinalIgnoreCase));

        if (image is null)
        {
            return;
        }

        image.IsAiProcessing = false;
        List<string> normalizedTags = NormalizeAppliedTags(message.GeneratedTags);
        image.Tags = normalizedTags;
        image.Status = normalizedTags.Count == 0 ? TagStatus.Untagged : TagStatus.AutoTagged;

        if (CurrentImage is null || !string.Equals(CurrentImage.FilePath, image.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        IsBusy = false;
        AppliedTags = new ObservableCollection<string>(normalizedTags);
        CurrentStatus = image.Status;
        StatusText = string.Format("AI tags refreshed for {0}.", image.FileName);
    }

    private void HandleAiTaggingFailed(AiTaggingFailedMessage message)
    {
        ImageEntry? image = ImageList.FirstOrDefault(candidate =>
            string.Equals(candidate.FilePath, message.ImagePath, StringComparison.OrdinalIgnoreCase));

        if (image is null)
        {
            return;
        }

        image.IsAiProcessing = false;
        image.Status = TagStatus.Untagged;

        if (CurrentImage is not null && string.Equals(CurrentImage.FilePath, image.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            IsBusy = false;
            CurrentStatus = image.Status;
        }

        StatusText = string.Format("AI tagging failed for {0}: {1}", image.FileName, message.ErrorMessage);
    }

    private async Task HandleImageMovedAsync(ImageMovedMessage message)
    {
        if (isIgnoringNextImageMovedMessage)
        {
            return;
        }

        if (CurrentImage is null || ActiveStage is null)
        {
            return;
        }

        bool affectsCurrentScreen = string.Equals(CurrentImage.FilePath, message.ImagePath, StringComparison.OrdinalIgnoreCase)
            || string.Equals(ActiveStage.FolderPath, message.SourceFolder, StringComparison.OrdinalIgnoreCase)
            || string.Equals(ActiveStage.FolderPath, message.TargetFolder, StringComparison.OrdinalIgnoreCase);

        if (!affectsCurrentScreen)
        {
            return;
        }

        await LoadStagesAsync(ActiveStage.FolderName, message.ImagePath, CurrentIndex).ConfigureAwait(true);
    }

    private void ReplaceImageList(IReadOnlyList<ImageEntry> images)
    {
        ImageList = new ObservableCollection<ImageEntry>(images);
        CurrentIndex = ImageList.Count == 0 ? -1 : CurrentIndex;
    }

    private int QueueAiTaggingForImages(IEnumerable<ImageEntry> images)
    {
        int queuedImageCount = 0;

        foreach (ImageEntry image in images)
        {
            if (QueueAiTaggingIfNeeded(image))
            {
                queuedImageCount++;
            }
        }

        return queuedImageCount;
    }

    private bool QueueAiTaggingIfNeeded(ImageEntry image)
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

    private void DisposeCurrentBitmap()
    {
        Bitmap? previousBitmap = CurrentImageSource;
        CurrentImageSource = null;
        previousBitmap?.Dispose();
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

    private HashSet<string> BuildNormalizedPrefixTagSet()
    {
        HashSet<string> prefixTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (currentProject is null)
        {
            return prefixTags;
        }

        foreach (string prefixTag in currentProject.PrefixTags)
        {
            if (string.IsNullOrWhiteSpace(prefixTag))
            {
                continue;
            }

            string resolvedPrefixTag = tagDictionaryService.ResolveAlias(currentProject.Id, prefixTag.Trim());
            if (!string.IsNullOrWhiteSpace(resolvedPrefixTag))
            {
                prefixTags.Add(resolvedPrefixTag);
            }
        }

        return prefixTags;
    }
}
