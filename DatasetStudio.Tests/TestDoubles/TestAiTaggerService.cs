using DatasetStudio.Messages;
using DatasetStudio.Models;
using DatasetStudio.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DatasetStudio.Tests.TestDoubles;

public sealed class TestAiTaggerService : IAiTaggerService
{
    private readonly HashSet<string> downloadingModels;
    private readonly Dictionary<string, string> requestedModelsByImagePath;
    private readonly HashSet<string> processingImages;

    public TestAiTaggerService()
    {
        downloadingModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        requestedModelsByImagePath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        processingImages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AvailableModels = Array.Empty<AiModelInfo>();
    }

    public event EventHandler<AiTaggingCompletedMessage>? TagGenerationCompleted;

    public IReadOnlyList<AiModelInfo> AvailableModels { get; set; }

    public IReadOnlyDictionary<string, string> RequestedModelsByImagePath
    {
        get
        {
            return requestedModelsByImagePath;
        }
    }

    public Task<IReadOnlyList<string>> GenerateTagsAsync(string imageFilePath, string modelName)
    {
        requestedModelsByImagePath[imageFilePath] = modelName;
        processingImages.Add(imageFilePath);
        return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }

    public bool TryQueueTagGeneration(Project project, string imageFilePath)
    {
        string? modelName = AiTaggingModelResolver.ResolveConfiguredModelName(project);
        if (string.IsNullOrWhiteSpace(modelName))
        {
            return false;
        }

        requestedModelsByImagePath[imageFilePath] = modelName;
        processingImages.Add(imageFilePath);
        return true;
    }

    public Task<IReadOnlyList<AiModelInfo>> GetAvailableModelsAsync()
    {
        return Task.FromResult(AvailableModels);
    }

    public Task<AiModelInfo?> DownloadModelAsync(string modelId, CancellationToken cancellationToken = default)
    {
        downloadingModels.Add(modelId);
        AiModelInfo? model = AvailableModels.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, modelId, StringComparison.OrdinalIgnoreCase));
        if (model is not null)
        {
            model.IsInstalled = true;
            if (string.IsNullOrWhiteSpace(model.ModelPath))
            {
                model.ModelPath = Path.Combine("C:\\models", model.Id.Replace('/', '_'));
            }
        }

        downloadingModels.Remove(modelId);
        return Task.FromResult(model);
    }

    public bool IsModelDownloadInProgress(string modelId)
    {
        return downloadingModels.Contains(modelId);
    }

    public bool IsProcessing(string imageFilePath)
    {
        return processingImages.Contains(imageFilePath);
    }

    public void Complete(string imageFilePath, IReadOnlyList<string> generatedTags)
    {
        processingImages.Remove(imageFilePath);
        TagGenerationCompleted?.Invoke(this, new AiTaggingCompletedMessage(imageFilePath, generatedTags));
    }
}
