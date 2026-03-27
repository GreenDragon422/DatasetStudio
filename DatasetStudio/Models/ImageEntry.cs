using System.Collections.Generic;

namespace DatasetStudio.Models;

public sealed class ImageEntry
{
    public string FilePath { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string TagFilePath { get; set; } = string.Empty;

    public TagStatus Status { get; set; }

    public List<string> Tags { get; set; } = new List<string>();

    public bool IsSelected { get; set; }

    public bool IsAiProcessing { get; set; }
}
