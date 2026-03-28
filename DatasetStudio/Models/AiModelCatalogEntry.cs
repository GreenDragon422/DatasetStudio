using System.Collections.Generic;

namespace DatasetStudio.Models;

public sealed class AiModelCatalogEntry
{
    public string Id { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string RepositoryId { get; set; } = string.Empty;

    public string Revision { get; set; } = string.Empty;

    public string ModelPath { get; set; } = string.Empty;

    public List<string> IncludePatterns { get; set; } = new List<string>();

    public List<string> ExcludePatterns { get; set; } = new List<string>();
}
