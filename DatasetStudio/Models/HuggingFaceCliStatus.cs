namespace DatasetStudio.Models;

public sealed class HuggingFaceCliStatus
{
    public bool IsAvailable { get; set; }

    public bool IsAppManaged { get; set; }

    public string ExecutablePath { get; set; } = string.Empty;

    public string StatusMessage { get; set; } = string.Empty;
}
