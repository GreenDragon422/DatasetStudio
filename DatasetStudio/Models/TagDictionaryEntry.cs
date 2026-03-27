using System.Collections.Generic;

namespace DatasetStudio.Models;

public sealed class TagDictionaryEntry
{
    public string CanonicalName { get; set; } = string.Empty;

    public List<string> Aliases { get; set; } = new List<string>();

    public int GlobalFrequency { get; set; }
}
