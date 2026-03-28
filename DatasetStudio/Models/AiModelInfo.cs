using System;
using System.Collections.Generic;

namespace DatasetStudio.Models;

public sealed class AiModelInfo
{
    public string Id { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string RepositoryId { get; set; } = string.Empty;

    public string Revision { get; set; } = string.Empty;

    public IReadOnlyList<string> IncludePatterns { get; set; } = Array.Empty<string>();

    public IReadOnlyList<string> ExcludePatterns { get; set; } = Array.Empty<string>();

    public string ModelPath { get; set; } = string.Empty;

    public bool IsInstalled { get; set; }

    public bool CanDownloadFromHuggingFace
    {
        get
        {
            return !string.IsNullOrWhiteSpace(RepositoryId);
        }
    }

    public string StatusLabel
    {
        get
        {
            if (IsInstalled && !string.IsNullOrWhiteSpace(ModelPath))
            {
                return string.Format("Installed · {0}", ModelPath);
            }

            if (CanDownloadFromHuggingFace)
            {
                return string.Format("Download from Hugging Face · {0}", RepositoryId);
            }

            if (!string.IsNullOrWhiteSpace(ModelPath))
            {
                return string.Format("Local model · {0}", ModelPath);
            }

            return "Model path not configured.";
        }
    }
}
