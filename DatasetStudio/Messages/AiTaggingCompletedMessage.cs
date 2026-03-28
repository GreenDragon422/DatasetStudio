using DatasetStudio.Models;
using System.Collections.Generic;

namespace DatasetStudio.Messages;

public sealed record AiTaggingCompletedMessage(string ImagePath, ImageTaggingResult Result)
{
    public IReadOnlyList<string> GeneratedTags
    {
        get
        {
            return Result.AcceptedTrainingTags;
        }
    }
}
