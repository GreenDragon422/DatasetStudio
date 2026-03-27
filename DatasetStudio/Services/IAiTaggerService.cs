using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DatasetStudio.Models;
using DatasetStudio.Messages;

namespace DatasetStudio.Services;

public interface IAiTaggerService
{
    Task<IReadOnlyList<string>> GenerateTagsAsync(string imageFilePath, string modelName);

    Task<IReadOnlyList<AiModelInfo>> GetAvailableModelsAsync();

    bool IsProcessing(string imageFilePath);

    event EventHandler<AiTaggingCompletedMessage>? TagGenerationCompleted;
}
