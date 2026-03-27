namespace DatasetStudio.Models;

public sealed class WorkflowStage
{
    public int Order { get; set; }

    public string FolderName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;
}
