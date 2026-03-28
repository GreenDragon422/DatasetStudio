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
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DatasetStudio.ViewModels;

public partial class ProjectConfigurationViewModel : ScreenViewModelBase
{
    private static readonly Regex PrefixTagPattern = new(@"^[\p{L}\p{N} _.-]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IAiTaggerService aiTaggerService;
    private readonly IFileSystemService fileSystemService;
    private readonly IProjectService projectService;
    private readonly IMessenger messenger;

    private Project? workingProject;

    public ProjectConfigurationViewModel(
        IProjectService projectService,
        IAiTaggerService aiTaggerService,
        IFileSystemService fileSystemService,
        IMessenger messenger)
        : base(messenger)
    {
        this.projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
        this.aiTaggerService = aiTaggerService ?? throw new ArgumentNullException(nameof(aiTaggerService));
        this.fileSystemService = fileSystemService ?? throw new ArgumentNullException(nameof(fileSystemService));
        this.messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));

        AvailableAiModels = new ObservableCollection<AiModelInfo>();
        Stages = new ObservableCollection<ProjectConfigurationStageViewModel>();
        StatusText = "Project configuration ready.";
    }

    public event EventHandler? CloseRequested;

    [ObservableProperty]
    private string projectName = string.Empty;

    [ObservableProperty]
    private string rootFolderPath = string.Empty;

    [ObservableProperty]
    private string selectedAiModelName = string.Empty;

    [ObservableProperty]
    private string prefixTagsText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<AiModelInfo> availableAiModels;

    [ObservableProperty]
    private ObservableCollection<ProjectConfigurationStageViewModel> stages;

    [ObservableProperty]
    private string prefixTagsError = string.Empty;

    [ObservableProperty]
    private bool hasPrefixTagsError;

    [ObservableProperty]
    private bool isLoadingAiModels;

    [ObservableProperty]
    private bool isSaving;

    [ObservableProperty]
    private string selectedAiModelStatusText = "No AI model selected.";

    [ObservableProperty]
    private bool isSelectedAiModelInstalled;

    [ObservableProperty]
    private bool isDownloadingSelectedAiModel;

    public bool CanSave
    {
        get
        {
            return !IsSaving
                && !HasPrefixTagsError
                && !string.IsNullOrWhiteSpace(ProjectName)
                && !string.IsNullOrWhiteSpace(RootFolderPath);
        }
    }

    public void LoadProject(Project project)
    {
        if (project is null)
        {
            throw new ArgumentNullException(nameof(project));
        }

        workingProject = CloneProject(project);

        ProjectName = workingProject.Name;
        RootFolderPath = workingProject.RootFolderPath;
        SelectedAiModelName = workingProject.AiModelName;
        PrefixTagsText = string.Join(", ", workingProject.PrefixTags);

        Stages.Clear();
        List<WorkflowStage> orderedStages = workingProject.Stages
            .OrderBy(stage => stage.Order)
            .ThenBy(stage => stage.FolderName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (WorkflowStage stage in orderedStages)
        {
            Stages.Add(new ProjectConfigurationStageViewModel(stage));
        }

        NormalizeStages();
        ValidatePrefixTags();
        RefreshSelectedAiModelState();
        OnPropertyChanged(nameof(CanSave));
    }

    [RelayCommand]
    private void BrowseRootFolder(string? folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return;
        }

        RootFolderPath = folderPath.Trim();

        if (string.IsNullOrWhiteSpace(ProjectName))
        {
            ProjectName = Path.GetFileName(RootFolderPath);
        }
    }

    [RelayCommand]
    private async Task LoadAiModelsAsync()
    {
        IsLoadingAiModels = true;

        try
        {
            IReadOnlyList<AiModelInfo> models = await aiTaggerService.GetAvailableModelsAsync().ConfigureAwait(true);
            AvailableAiModels.Clear();

            foreach (AiModelInfo model in models)
            {
                AvailableAiModels.Add(model);
            }

            if (string.IsNullOrWhiteSpace(SelectedAiModelName) && AvailableAiModels.Count > 0)
            {
                SelectedAiModelName = AvailableAiModels[0].Id;
            }

            RefreshSelectedAiModelState();
            StatusText = AvailableAiModels.Count > 0
                ? string.Format("Loaded {0} AI model{1}.", AvailableAiModels.Count, AvailableAiModels.Count == 1 ? string.Empty : "s")
                : "No AI models were found.";
        }
        catch (Exception exception)
        {
            StatusText = string.Format("Could not load AI models: {0}", exception.Message);
        }
        finally
        {
            IsLoadingAiModels = false;
        }
    }

    [RelayCommand]
    private async Task DownloadSelectedAiModelAsync()
    {
        AiModelInfo? selectedModel = AvailableAiModels.FirstOrDefault(model =>
            string.Equals(model.Id, SelectedAiModelName, StringComparison.OrdinalIgnoreCase));
        if (selectedModel is null)
        {
            SelectedAiModelStatusText = "Select a catalog model before downloading.";
            return;
        }

        if (!selectedModel.CanDownloadFromHuggingFace)
        {
            SelectedAiModelStatusText = "The selected model does not define a Hugging Face download source.";
            return;
        }

        IsDownloadingSelectedAiModel = true;
        SelectedAiModelStatusText = string.Format("Downloading {0}...", selectedModel.DisplayName);
        OnPropertyChanged(nameof(CanDownloadSelectedAiModel));
        OnPropertyChanged(nameof(DownloadSelectedAiModelButtonText));

        try
        {
            AiModelInfo? downloadedModel = await aiTaggerService.DownloadModelAsync(selectedModel.Id).ConfigureAwait(true);
            await LoadAiModelsAsync().ConfigureAwait(true);
            if (downloadedModel is null)
            {
                StatusText = string.Format("Model {0} is not in the current AI model catalog.", selectedModel.Id);
                SelectedAiModelStatusText = "Model catalog entry was not found after download.";
                return;
            }

            SelectedAiModelName = downloadedModel.Id;
            StatusText = string.Format("Downloaded {0}.", downloadedModel.DisplayName);
        }
        catch (Exception exception)
        {
            StatusText = string.Format("Could not download the selected AI model: {0}", exception.Message);
            SelectedAiModelStatusText = StatusText;
        }
        finally
        {
            IsDownloadingSelectedAiModel = false;
            RefreshSelectedAiModelState();
            OnPropertyChanged(nameof(CanDownloadSelectedAiModel));
            OnPropertyChanged(nameof(DownloadSelectedAiModelButtonText));
        }
    }

    [RelayCommand]
    private void AddStage()
    {
        ProjectConfigurationStageViewModel stage = new()
        {
            FolderName = "Stage",
            DisplayName = "Stage",
        };

        Stages.Add(stage);
        NormalizeStages();
        OnPropertyChanged(nameof(CanSave));
    }

    [RelayCommand]
    private void RemoveStage(ProjectConfigurationStageViewModel? stage)
    {
        if (stage is null)
        {
            return;
        }

        if (!Stages.Remove(stage))
        {
            return;
        }

        NormalizeStages();
        OnPropertyChanged(nameof(CanSave));
    }

    [RelayCommand]
    private void MoveStageUp(ProjectConfigurationStageViewModel? stage)
    {
        if (stage is null)
        {
            return;
        }

        int stageIndex = Stages.IndexOf(stage);
        if (stageIndex <= 0)
        {
            return;
        }

        Stages.Move(stageIndex, stageIndex - 1);
        NormalizeStages();
    }

    [RelayCommand]
    private void MoveStageDown(ProjectConfigurationStageViewModel? stage)
    {
        if (stage is null)
        {
            return;
        }

        int stageIndex = Stages.IndexOf(stage);
        if (stageIndex < 0 || stageIndex >= Stages.Count - 1)
        {
            return;
        }

        Stages.Move(stageIndex, stageIndex + 1);
        NormalizeStages();
    }

    public void MoveStageBefore(ProjectConfigurationStageViewModel? sourceStage, ProjectConfigurationStageViewModel? targetStage)
    {
        if (sourceStage is null || targetStage is null || ReferenceEquals(sourceStage, targetStage))
        {
            return;
        }

        int sourceIndex = Stages.IndexOf(sourceStage);
        int targetIndex = Stages.IndexOf(targetStage);

        if (sourceIndex < 0 || targetIndex < 0)
        {
            return;
        }

        Stages.Move(sourceIndex, sourceIndex < targetIndex ? targetIndex - 1 : targetIndex);
        NormalizeStages();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!ValidateBeforeSave())
        {
            return;
        }

        IsSaving = true;

        try
        {
            Project project = workingProject ?? new Project();
            project.Name = ProjectName.Trim();
            project.RootFolderPath = RootFolderPath.Trim();
            project.AiModelName = SelectedAiModelName.Trim();
            project.PrefixTags = ParsePrefixTags(PrefixTagsText);
            project.Stages = Stages.Select(stage => stage.ToWorkflowStage()).ToList();

            if (project.State is null)
            {
                project.State = new ProjectState();
            }

            project.State.SelectedAiModelName = project.AiModelName;

            if (project.Stages.Count > 0)
            {
                bool stageMissingFromState = string.IsNullOrWhiteSpace(project.State.ActiveStageFolderName)
                    || project.Stages.All(stage => stage.FolderName != project.State.ActiveStageFolderName);

                if (stageMissingFromState)
                {
                    project.State.ActiveStageFolderName = project.Stages[0].FolderName;
                }
            }

            await projectService.SaveProjectAsync(project).ConfigureAwait(false);

            foreach (WorkflowStage stage in project.Stages)
            {
                string stageFolderPath = Path.Combine(project.RootFolderPath, stage.FolderName);
                await fileSystemService.EnsureFolderExistsAsync(stageFolderPath).ConfigureAwait(false);
            }

            workingProject = CloneProject(project);
            messenger.Send(new ProjectConfigSavedMessage(project.Id));
            StatusText = "Project configuration saved.";
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exception)
        {
            StatusText = string.Format("Could not save project: {0}", exception.Message);
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    partial void OnProjectNameChanged(string value)
    {
        _ = value;
        OnPropertyChanged(nameof(CanSave));
    }

    partial void OnRootFolderPathChanged(string value)
    {
        _ = value;
        OnPropertyChanged(nameof(CanSave));
    }

    partial void OnSelectedAiModelNameChanged(string value)
    {
        _ = value;
        RefreshSelectedAiModelState();
    }

    partial void OnPrefixTagsTextChanged(string value)
    {
        _ = value;
        ValidatePrefixTags();
        OnPropertyChanged(nameof(CanSave));
    }

    partial void OnHasPrefixTagsErrorChanged(bool value)
    {
        _ = value;
        OnPropertyChanged(nameof(CanSave));
    }

    partial void OnIsSavingChanged(bool value)
    {
        _ = value;
        OnPropertyChanged(nameof(CanSave));
    }

    public bool CanDownloadSelectedAiModel
    {
        get
        {
            AiModelInfo? selectedModel = AvailableAiModels.FirstOrDefault(model =>
                string.Equals(model.Id, SelectedAiModelName, StringComparison.OrdinalIgnoreCase));
            return !IsLoadingAiModels
                && !IsDownloadingSelectedAiModel
                && selectedModel is not null
                && selectedModel.CanDownloadFromHuggingFace
                && !selectedModel.IsInstalled;
        }
    }

    public string DownloadSelectedAiModelButtonText
    {
        get
        {
            return IsDownloadingSelectedAiModel
                ? "Downloading..."
                : (IsSelectedAiModelInstalled ? "Installed" : "Download Model");
        }
    }

    private bool ValidateBeforeSave()
    {
        if (string.IsNullOrWhiteSpace(ProjectName))
        {
            StatusText = "Project name is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(RootFolderPath))
        {
            StatusText = "Root folder path is required.";
            return false;
        }

        ValidatePrefixTags();
        if (HasPrefixTagsError)
        {
            StatusText = PrefixTagsError;
            return false;
        }

        NormalizeStages();
        return true;
    }

    private void ValidatePrefixTags()
    {
        if (string.IsNullOrWhiteSpace(PrefixTagsText))
        {
            PrefixTagsError = string.Empty;
            HasPrefixTagsError = false;
            return;
        }

        string[] prefixTags = PrefixTagsText.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        foreach (string prefixTag in prefixTags)
        {
            if (!PrefixTagPattern.IsMatch(prefixTag))
            {
                PrefixTagsError = "Prefix tags can only contain letters, numbers, spaces, underscores, periods, and hyphens.";
                HasPrefixTagsError = true;
                return;
            }
        }

        PrefixTagsError = string.Empty;
        HasPrefixTagsError = false;
    }

    private void NormalizeStages()
    {
        for (int index = 0; index < Stages.Count; index++)
        {
            Stages[index].Order = index;
            Stages[index].NormalizeFolderName();
        }
    }

    private static Project CloneProject(Project project)
    {
        return new Project
        {
            Id = project.Id,
            Name = project.Name,
            RootFolderPath = project.RootFolderPath,
            Stages = project.Stages.Select(stage => new WorkflowStage
            {
                Order = stage.Order,
                FolderName = stage.FolderName,
                DisplayName = stage.DisplayName,
            }).ToList(),
            PrefixTags = project.PrefixTags.ToList(),
            AiModelName = project.AiModelName,
            LastModified = project.LastModified,
            TagDictionaryEntries = project.TagDictionaryEntries.Select(entry => new TagDictionaryEntry
            {
                CanonicalName = entry.CanonicalName,
                Aliases = entry.Aliases.ToList(),
                GlobalFrequency = entry.GlobalFrequency,
            }).ToList(),
            State = project.State is null
                ? new ProjectState()
                : new ProjectState
                {
                    ActiveStageFolderName = project.State.ActiveStageFolderName,
                    ZoomSliderValue = project.State.ZoomSliderValue,
                    SelectedAiModelName = project.State.SelectedAiModelName,
                    LastInspectedImagePath = project.State.LastInspectedImagePath,
                },
        };
    }

    private static List<string> ParsePrefixTags(string prefixTagsText)
    {
        if (string.IsNullOrWhiteSpace(prefixTagsText))
        {
            return [];
        }

        return prefixTagsText
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToList();
    }

    private void RefreshSelectedAiModelState()
    {
        AiModelInfo? selectedModel = AvailableAiModels.FirstOrDefault(model =>
            string.Equals(model.Id, SelectedAiModelName, StringComparison.OrdinalIgnoreCase));
        if (selectedModel is null)
        {
            IsSelectedAiModelInstalled = false;
            IsDownloadingSelectedAiModel = !string.IsNullOrWhiteSpace(SelectedAiModelName)
                && aiTaggerService.IsModelDownloadInProgress(SelectedAiModelName);
            SelectedAiModelStatusText = string.IsNullOrWhiteSpace(SelectedAiModelName)
                ? "No AI model selected."
                : "Selected model is not present in the current catalog.";
            OnPropertyChanged(nameof(CanDownloadSelectedAiModel));
            OnPropertyChanged(nameof(DownloadSelectedAiModelButtonText));
            return;
        }

        IsSelectedAiModelInstalled = selectedModel.IsInstalled;
        IsDownloadingSelectedAiModel = aiTaggerService.IsModelDownloadInProgress(selectedModel.Id);
        SelectedAiModelStatusText = IsDownloadingSelectedAiModel
            ? string.Format("Downloading {0}...", selectedModel.DisplayName)
            : selectedModel.StatusLabel;
        OnPropertyChanged(nameof(CanDownloadSelectedAiModel));
        OnPropertyChanged(nameof(DownloadSelectedAiModelButtonText));
    }
}
