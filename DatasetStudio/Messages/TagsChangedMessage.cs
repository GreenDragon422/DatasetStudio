using System.Collections.Generic;

namespace DatasetStudio.Messages;

public sealed record TagsChangedMessage(string ImagePath, IReadOnlyList<string> NewTags);
