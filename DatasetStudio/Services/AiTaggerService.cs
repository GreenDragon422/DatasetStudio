using DatasetStudio.Messages;
using DatasetStudio.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace DatasetStudio.Services;

public sealed class AiTaggerService : IAiTaggerService
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly ConcurrentDictionary<string, byte> processingImages;
    private readonly IStatePersistenceService statePersistenceService;

    public AiTaggerService(IStatePersistenceService statePersistenceService)
    {
        this.statePersistenceService = statePersistenceService ?? throw new ArgumentNullException(nameof(statePersistenceService));
        processingImages = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
    }

    public event EventHandler<AiTaggingCompletedMessage>? TagGenerationCompleted;

    public async Task<IReadOnlyList<string>> GenerateTagsAsync(string imageFilePath, string modelName)
    {
        if (string.IsNullOrWhiteSpace(imageFilePath))
        {
            throw new ArgumentException("Image file path is required.", nameof(imageFilePath));
        }

        processingImages[imageFilePath] = 0;

        try
        {
            await Task.Delay(50).ConfigureAwait(false);
            List<string> generatedTags = BuildPlaceholderTags(imageFilePath, modelName);
            TagGenerationCompleted?.Invoke(this, new AiTaggingCompletedMessage(imageFilePath, generatedTags));
            return generatedTags;
        }
        finally
        {
            processingImages.TryRemove(imageFilePath, out _);
        }
    }

    public async Task<IReadOnlyList<AiModelInfo>> GetAvailableModelsAsync()
    {
        string? aiModelsPath = await ResolveAiModelsPathAsync();

        if (string.IsNullOrWhiteSpace(aiModelsPath) || !File.Exists(aiModelsPath))
        {
            return [];
        }

        try
        {
            await using FileStream fileStream = File.OpenRead(aiModelsPath);
            List<AiModelInfo>? models = await JsonSerializer.DeserializeAsync<List<AiModelInfo>>(fileStream, JsonSerializerOptions).ConfigureAwait(false);
            return models ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public bool IsProcessing(string imageFilePath)
    {
        return processingImages.ContainsKey(imageFilePath);
    }

    private async Task<string?> ResolveAiModelsPathAsync()
    {
        AppState appState = await statePersistenceService.LoadAppStateAsync().ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(appState.LastMasterRootDirectory))
        {
            return Path.Combine(appState.LastMasterRootDirectory, "ai_models.json");
        }

        return Path.Combine(AppContext.BaseDirectory, "ai_models.json");
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
