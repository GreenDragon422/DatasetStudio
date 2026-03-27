using System.Collections.Generic;

namespace DatasetStudio.Messages;

public sealed record AiTaggingCompletedMessage(string ImagePath, IReadOnlyList<string> GeneratedTags);
