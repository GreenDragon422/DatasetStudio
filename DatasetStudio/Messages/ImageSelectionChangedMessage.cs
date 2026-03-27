namespace DatasetStudio.Messages;

public sealed record ImageSelectionChangedMessage(string ImagePath, bool IsSelected);
