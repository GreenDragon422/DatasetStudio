namespace DatasetStudio.Models;

public sealed class ImageTagScore
{
    public string SourceName { get; set; } = string.Empty;

    public string ExportName { get; set; } = string.Empty;

    public ImageTagCategory Category { get; set; }

    public float Score { get; set; }
}
