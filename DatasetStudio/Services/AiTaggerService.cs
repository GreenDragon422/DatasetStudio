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
    private readonly ITaggerSession taggerSession;
    private readonly ConcurrentDictionary<string, byte> processingImages;
    private readonly ConcurrentDictionary<string, Task<AiModelInfo?>> modelDownloadsById;
    private readonly IMessenger messenger;

    public AiTaggerService(
        IAiModelCatalogService aiModelCatalogService,
        IHuggingFaceCliService huggingFaceCliService,
        ITaggerSession taggerSession,
        IMessenger messenger)
    {
        this.aiModelCatalogService = aiModelCatalogService ?? throw new ArgumentNullException(nameof(aiModelCatalogService));
        this.huggingFaceCliService = huggingFaceCliService ?? throw new ArgumentNullException(nameof(huggingFaceCliService));
        this.taggerSession = taggerSession ?? throw new ArgumentNullException(nameof(taggerSession));
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

        ImageTaggingResult taggingResult = await GenerateTaggingResultAsync(imageFilePath, modelName).ConfigureAwait(false);
        return taggingResult.AcceptedTrainingTags;
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
            ImageTaggingResult taggingResult = await GenerateTaggingResultAsync(imageFilePath, modelName).ConfigureAwait(false);
            taggingResult.AcceptedTrainingTags = NormalizeGeneratedTags(project, taggingResult.AcceptedTrainingTags);
            TagGenerationCompleted?.Invoke(this, new AiTaggingCompletedMessage(imageFilePath, taggingResult));
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

    private async Task<ImageTaggingResult> GenerateTaggingResultAsync(
        string imageFilePath,
        string modelName,
        CancellationToken cancellationToken = default)
    {
        TaggerModelConfig modelConfig = await BuildModelConfigAsync(modelName, cancellationToken).ConfigureAwait(false);
        ImageTaggingResult taggingResult = await taggerSession
            .TagImageAsync(modelConfig, imageFilePath, cancellationToken)
            .ConfigureAwait(false);
        return taggingResult;
    }

    private async Task<TaggerModelConfig> BuildModelConfigAsync(
        string modelName,
        CancellationToken cancellationToken)
    {
        AiModelInfo? model = await aiModelCatalogService.GetModelAsync(modelName, cancellationToken).ConfigureAwait(false);
        if (model is null)
        {
            throw new InvalidOperationException(string.Format("Model '{0}' is not present in the current AI model catalog.", modelName));
        }

        if (!model.IsInstalled)
        {
            string installationMessage = model.CanDownloadFromHuggingFace
                ? string.Format(
                    "Model '{0}' is not installed. Use the Download Model button next to the AI model selector before tagging.",
                    model.DisplayName)
                : string.Format(
                    "Model '{0}' is not installed or its configured local path is unavailable.",
                    model.DisplayName);
            throw new InvalidOperationException(installationMessage);
        }

        string modelRootPath = model.ModelPath;
        string modelFilePath = ResolveRequiredModelFilePath(modelRootPath, "model.onnx");
        string tagCsvPath = ResolveRequiredModelFilePath(modelRootPath, "selected_tags.csv");

        return new TaggerModelConfig
        {
            ModelId = model.Id,
            DisplayName = model.DisplayName,
            ModelFilePath = modelFilePath,
            TagCsvPath = tagCsvPath,
            BatchSize = 32,
            GeneralThreshold = 0.35f,
            CharacterThreshold = 0.85f,
        };
    }

    private static string ResolveRequiredModelFilePath(string modelRootPath, string expectedFileName)
    {
        if (File.Exists(modelRootPath))
        {
            string actualFileName = Path.GetFileName(modelRootPath);
            if (!string.Equals(actualFileName, expectedFileName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(string.Format(
                    "The installed model path points to '{0}', but DatasetStudio expected '{1}'.",
                    actualFileName,
                    expectedFileName));
            }

            return modelRootPath;
        }

        if (Directory.Exists(modelRootPath))
        {
            string candidateFilePath = Path.Combine(modelRootPath, expectedFileName);
            if (File.Exists(candidateFilePath))
            {
                return candidateFilePath;
            }
        }

        throw new InvalidOperationException(string.Format(
            "The installed model at '{0}' does not contain the required file '{1}'. WD-style ONNX taggers require both model.onnx and selected_tags.csv.",
            modelRootPath,
            expectedFileName));
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
}
