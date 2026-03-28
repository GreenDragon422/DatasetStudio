namespace DatasetStudio.Models;

public sealed class ProcessExecutionResult
{
    public int ExitCode { get; set; }

    public string StandardOutput { get; set; } = string.Empty;

    public string StandardError { get; set; } = string.Empty;
}
