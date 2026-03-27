using DatasetStudio.Messages;
using DatasetStudio.Models;
using DatasetStudio.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DatasetStudio.Tests.TestDoubles;

public sealed class TestAiTaggerService : IAiTaggerService
{
    private readonly Dictionary<string, string> requestedModelsByImagePath;
    private readonly HashSet<string> processingImages;

    public TestAiTaggerService()
    {
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
