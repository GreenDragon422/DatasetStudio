namespace DatasetStudio.Models;

public sealed class TaggerLabelDefinition
{
    public int Index { get; set; }

    public string SourceName { get; set; } = string.Empty;

    public string ExportName { get; set; } = string.Empty;

    public ImageTagCategory Category { get; set; }

    public long SampleCount { get; set; }
}
