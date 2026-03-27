namespace DatasetStudio.Models;

public sealed class AppState
{
    public string? LastOpenedProjectId { get; set; }

    public double WindowWidth { get; set; }

    public double WindowHeight { get; set; }

    public double WindowX { get; set; }

    public double WindowY { get; set; }

    public string? LastMasterRootDirectory { get; set; }
}
