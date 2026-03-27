namespace DatasetStudio.Models;

public sealed class ProjectState
{
    public string? ActiveStageFolderName { get; set; }

    public int ZoomSliderValue { get; set; }

    public string? SelectedAiModelName { get; set; }

    public string? LastInspectedImagePath { get; set; }
}
