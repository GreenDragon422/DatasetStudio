namespace DatasetStudio.Messages;

public sealed record AiTaggingFailedMessage(string ImagePath, string ErrorMessage);
