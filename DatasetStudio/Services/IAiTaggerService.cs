using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DatasetStudio.Models;
using DatasetStudio.Messages;

namespace DatasetStudio.Services;

public interface IAiTaggerService
{
    Task<IReadOnlyList<string>> GenerateTagsAsync(string imageFilePath, string modelName);

    bool TryQueueTagGeneration(Project project, string imageFilePath);

    Task<IReadOnlyList<AiModelInfo>> GetAvailableModelsAsync();

    Task<AiModelInfo?> DownloadModelAsync(string modelId, CancellationToken cancellationToken = default);

    bool IsModelDownloadInProgress(string modelId);

    bool IsProcessing(string imageFilePath);

    event EventHandler<AiTaggingCompletedMessage>? TagGenerationCompleted;
}
