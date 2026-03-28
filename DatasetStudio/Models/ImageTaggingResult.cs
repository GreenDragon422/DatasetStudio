using System;
using System.Collections.Generic;
using System.Linq;

namespace DatasetStudio.Models;

public sealed class ImageTaggingResult
{
    public string ModelId { get; set; } = string.Empty;

    public IReadOnlyList<ImageTagScore> RatingTags { get; set; } = Array.Empty<ImageTagScore>();

    public IReadOnlyList<ImageTagScore> GeneralTags { get; set; } = Array.Empty<ImageTagScore>();

    public IReadOnlyList<ImageTagScore> CharacterTags { get; set; } = Array.Empty<ImageTagScore>();

    public string SelectedRating { get; set; } = string.Empty;

    public IReadOnlyList<string> AcceptedTrainingTags { get; set; } = Array.Empty<string>();

    public IReadOnlyList<ImageTagScore> AcceptedTags
    {
        get
        {
            return GeneralTags
                .Concat(CharacterTags)
                .OrderByDescending(tag => tag.Score)
                .ToList();
        }
    }
}
