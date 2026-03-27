namespace DatasetStudio.Messages;

public sealed record ImageMovedMessage(string ImagePath, string SourceFolder, string TargetFolder);
