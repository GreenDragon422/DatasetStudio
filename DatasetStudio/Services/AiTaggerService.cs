using CommunityToolkit.Mvvm.Messaging;
using DatasetStudio.Messages;
using DatasetStudio.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DatasetStudio.Services;

public sealed class AiTaggerService : IAiTaggerService
{
    private readonly IAiModelCatalogService aiModelCatalogService;
    private readonly IHuggingFaceCliService huggingFaceCliService;
    private readonly ConcurrentDictionary<string, byte> processingImages;
    private readonly ConcurrentDictionary<string, Task<AiModelInfo?>> modelDownloadsById;
    private readonly IMessenger messenger;

    public AiTaggerService(
        IAiModelCatalogService aiModelCatalogService,
        IHuggingFaceCliService huggingFaceCliService,
        IMessenger messenger)
    {
        this.aiModelCatalogService = aiModelCatalogService ?? throw new ArgumentNullException(nameof(aiModelCatalogService));
        this.huggingFaceCliService = huggingFaceCliService ?? throw new ArgumentNullException(nameof(huggingFaceCliService));
        this.messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        processingImages = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        modelDownloadsById = new ConcurrentDictionary<string, Task<AiModelInfo?>>(StringComparer.OrdinalIgnoreCase);
    }

    public event EventHandler<AiTaggingCompletedMessage>? TagGenerationCompleted;

    public async Task<IReadOnlyList<string>> GenerateTagsAsync(string imageFilePath, string modelName)
    {
        if (string.IsNullOrWhiteSpace(imageFilePath))
        {
            throw new ArgumentException("Image file path is required.", nameof(imageFilePath));
        }

        if (string.IsNullOrWhiteSpace(modelName))
        {
            throw new ArgumentException("An AI model name is required.", nameof(modelName));
        }

        AiModelInfo? model = await aiModelCatalogService.GetModelAsync(modelName).ConfigureAwait(false);
        if (model is not null && !model.IsInstalled)
        {
            AiModelInfo? installedModel = await DownloadModelAsync(model.Id).ConfigureAwait(false);
            if (installedModel is not null && !installedModel.IsInstalled)
            {
                throw new InvalidOperationException(string.Format("Model '{0}' is not installed.", installedModel.DisplayName));
            }
        }

        await Task.Delay(50).ConfigureAwait(false);
        return BuildPlaceholderTags(imageFilePath, modelName);
    }

    public bool TryQueueTagGeneration(Project project, string imageFilePath)
    {
        if (project is null)
        {
            throw new ArgumentNullException(nameof(project));
        }

        if (string.IsNullOrWhiteSpace(imageFilePath))
        {
            throw new ArgumentException("Image file path is required.", nameof(imageFilePath));
        }

        string? modelName = AiTaggingModelResolver.ResolveConfiguredModelName(project);
        if (string.IsNullOrWhiteSpace(modelName))
        {
            return false;
        }

        bool wasQueued = processingImages.TryAdd(imageFilePath, 0);
        if (!wasQueued)
        {
            return false;
        }

        _ = ProcessQueuedImageAsync(project, imageFilePath, modelName);
        return true;
    }

    public async Task<IReadOnlyList<AiModelInfo>> GetAvailableModelsAsync()
    {
        return await aiModelCatalogService.GetAvailableModelsAsync().ConfigureAwait(false);
    }

    public bool IsModelDownloadInProgress(string modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return false;
        }

        return modelDownloadsById.ContainsKey(modelId);
    }

    public Task<AiModelInfo?> DownloadModelAsync(string modelId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            throw new ArgumentException("An AI model id is required.", nameof(modelId));
        }

        Task<AiModelInfo?> downloadTask = modelDownloadsById.GetOrAdd(
            modelId,
            static (requestedModelId, state) => state.service.DownloadModelCoreAsync(requestedModelId, state.cancellationToken),
            (service: this, cancellationToken));

        return AwaitModelDownloadAsync(modelId, downloadTask);
    }

    private async Task<AiModelInfo?> AwaitModelDownloadAsync(string modelId, Task<AiModelInfo?> downloadTask)
    {
        try
        {
            return await downloadTask.ConfigureAwait(false);
        }
        finally
        {
            if (downloadTask.IsCompleted)
            {
                modelDownloadsById.TryRemove(modelId, out _);
            }
        }
    }

    public bool IsProcessing(string imageFilePath)
    {
        return processingImages.ContainsKey(imageFilePath);
    }

    private async Task ProcessQueuedImageAsync(Project project, string imageFilePath, string modelName)
    {
        try
        {
            IReadOnlyList<string> generatedTags = await GenerateTagsAsync(imageFilePath, modelName).ConfigureAwait(false);
            List<string> normalizedTags = NormalizeGeneratedTags(project, generatedTags);
            TagGenerationCompleted?.Invoke(this, new AiTaggingCompletedMessage(imageFilePath, normalizedTags));
        }
        catch (Exception exception)
        {
            messenger.Send(new AiTaggingFailedMessage(imageFilePath, exception.Message));
        }
        finally
        {
            processingImages.TryRemove(imageFilePath, out _);
        }
    }

    private async Task<AiModelInfo?> DownloadModelCoreAsync(string modelId, CancellationToken cancellationToken)
    {
        AiModelInfo? model = await aiModelCatalogService.GetModelAsync(modelId, cancellationToken).ConfigureAwait(false);
        if (model is null)
        {
            return null;
        }

        if (model.IsInstalled || !model.CanDownloadFromHuggingFace)
        {
            return model;
        }

        await huggingFaceCliService.DownloadModelAsync(model, cancellationToken).ConfigureAwait(false);
        return await aiModelCatalogService.GetModelAsync(modelId, cancellationToken).ConfigureAwait(false);
    }

    private static List<string> NormalizeGeneratedTags(Project project, IReadOnlyList<string> sourceTags)
    {
        HashSet<string> prefixTagSet = new HashSet<string>(
            project.PrefixTags
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Select(tag => tag.Trim()),
            StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> aliasLookup = BuildAliasLookup(project);
        List<string> normalizedTags = new List<string>();

        foreach (string sourceTag in sourceTags)
        {
            string trimmedTag = sourceTag.Trim();
            if (string.IsNullOrWhiteSpace(trimmedTag) || prefixTagSet.Contains(trimmedTag))
            {
                continue;
            }

            string resolvedTag = aliasLookup.TryGetValue(trimmedTag, out string? canonicalName)
                ? canonicalName
                : trimmedTag;
            if (normalizedTags.Any(existingTag => string.Equals(existingTag, resolvedTag, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            normalizedTags.Add(resolvedTag);
        }

        return normalizedTags;
    }

    private static Dictionary<string, string> BuildAliasLookup(Project project)
    {
        Dictionary<string, string> aliasLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (TagDictionaryEntry tagDictionaryEntry in project.TagDictionaryEntries)
        {
            if (string.IsNullOrWhiteSpace(tagDictionaryEntry.CanonicalName))
            {
                continue;
            }

            string canonicalName = tagDictionaryEntry.CanonicalName.Trim();
            aliasLookup[canonicalName] = canonicalName;

            foreach (string alias in tagDictionaryEntry.Aliases)
            {
                if (!string.IsNullOrWhiteSpace(alias))
                {
                    aliasLookup[alias.Trim()] = canonicalName;
                }
            }
        }

        return aliasLookup;
    }

    private static List<string> BuildPlaceholderTags(string imageFilePath, string modelName)
    {
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(imageFilePath);
        List<string> tags = fileNameWithoutExtension
            .Split(new[] { '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => segment.Trim().ToLowerInvariant())
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (tags.Count == 0)
        {
            tags.Add("auto_tagged");
        }

        if (!string.IsNullOrWhiteSpace(modelName))
        {
            tags.Add($"model:{modelName.Trim().ToLowerInvariant()}");
        }

        return tags;
    }
}
