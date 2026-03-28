using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatasetStudio.Models;
using System;
using System.Text.RegularExpressions;

namespace DatasetStudio.ViewModels;

public partial class ProjectConfigurationStageViewModel : ObservableObject
{
    private static readonly Regex NumericPrefixPattern = new(@"^(?<order>\d+)[_-](?<name>.+)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private Action<ProjectConfigurationStageViewModel>? moveStageUpAction;
    private Action<ProjectConfigurationStageViewModel>? moveStageDownAction;
    private Action<ProjectConfigurationStageViewModel>? removeStageAction;

    public ProjectConfigurationStageViewModel()
    {
        MoveStageUpCommand = new RelayCommand(ExecuteMoveStageUp);
        MoveStageDownCommand = new RelayCommand(ExecuteMoveStageDown);
        RemoveStageCommand = new RelayCommand(ExecuteRemoveStage);
    }

    public ProjectConfigurationStageViewModel(WorkflowStage stage)
        : this()
    {
        if (stage is null)
        {
            throw new ArgumentNullException(nameof(stage));
        }

        order = stage.Order;
        folderName = stage.FolderName;
        displayName = string.IsNullOrWhiteSpace(stage.DisplayName) ? StripNumericPrefix(stage.FolderName) : stage.DisplayName;
    }

    [ObservableProperty]
    private int order;

    [ObservableProperty]
    private string folderName = string.Empty;

    [ObservableProperty]
    private string displayName = string.Empty;

    public string OrderDisplay
    {
        get
        {
            return (Order + 1).ToString();
        }
    }

    public IRelayCommand MoveStageUpCommand { get; }

    public IRelayCommand MoveStageDownCommand { get; }

    public IRelayCommand RemoveStageCommand { get; }

    partial void OnOrderChanged(int value)
    {
        _ = value;
        OnPropertyChanged(nameof(OrderDisplay));
    }

    public void ConfigureCommands(
        Action<ProjectConfigurationStageViewModel> moveStageUpAction,
        Action<ProjectConfigurationStageViewModel> moveStageDownAction,
        Action<ProjectConfigurationStageViewModel> removeStageAction)
    {
        this.moveStageUpAction = moveStageUpAction ?? throw new ArgumentNullException(nameof(moveStageUpAction));
        this.moveStageDownAction = moveStageDownAction ?? throw new ArgumentNullException(nameof(moveStageDownAction));
        this.removeStageAction = removeStageAction ?? throw new ArgumentNullException(nameof(removeStageAction));
    }

    public void NormalizeFolderName()
    {
        string stageName = StripNumericPrefix(FolderName);

        if (string.IsNullOrWhiteSpace(stageName))
        {
            stageName = string.IsNullOrWhiteSpace(DisplayName) ? "Stage" : DisplayName.Trim();
        }

        FolderName = string.Format("{0:00}_{1}", Order + 1, stageName.Trim());
    }

    public WorkflowStage ToWorkflowStage()
    {
        return new WorkflowStage
        {
            Order = Order,
            FolderName = FolderName.Trim(),
            DisplayName = string.IsNullOrWhiteSpace(DisplayName) ? StripNumericPrefix(FolderName) : DisplayName.Trim(),
        };
    }

    private static string StripNumericPrefix(string folderName)
    {
        if (string.IsNullOrWhiteSpace(folderName))
        {
            return string.Empty;
        }

        Match match = NumericPrefixPattern.Match(folderName.Trim());
        if (!match.Success)
        {
            return folderName.Trim();
        }

        return match.Groups["name"].Value.Trim();
    }

    private void ExecuteMoveStageUp()
    {
        if (moveStageUpAction is not null)
        {
            moveStageUpAction(this);
        }
    }

    private void ExecuteMoveStageDown()
    {
        if (moveStageDownAction is not null)
        {
            moveStageDownAction(this);
        }
    }

    private void ExecuteRemoveStage()
    {
        if (removeStageAction is not null)
        {
            removeStageAction(this);
        }
    }
}
