using System;

namespace DatasetStudio.Models;

public sealed class TaggerModelConfig
{
    public string ModelId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string ModelFilePath { get; set; } = string.Empty;

    public string TagCsvPath { get; set; } = string.Empty;

    public int BatchSize { get; set; } = 32;

    public float GeneralThreshold { get; set; } = 0.35f;

    public float CharacterThreshold { get; set; } = 0.85f;

    public string CacheKey
    {
        get
        {
            return string.Format(
                "{0}|{1}|{2}|{3}|{4}",
                ModelFilePath,
                TagCsvPath,
                BatchSize,
                GeneralThreshold,
                CharacterThreshold);
        }
    }
}
