using DatasetStudio.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DatasetStudio.Services;

public sealed class AiModelCatalogService : IAiModelCatalogService
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly IHuggingFaceCliService huggingFaceCliService;
    private readonly IStatePersistenceService statePersistenceService;

    public AiModelCatalogService(
        IStatePersistenceService statePersistenceService,
        IHuggingFaceCliService huggingFaceCliService)
    {
        this.statePersistenceService = statePersistenceService ?? throw new ArgumentNullException(nameof(statePersistenceService));
        this.huggingFaceCliService = huggingFaceCliService ?? throw new ArgumentNullException(nameof(huggingFaceCliService));
    }

    public async Task<IReadOnlyList<AiModelInfo>> GetAvailableModelsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string catalogPath = await ResolveAiModelsPathAsync().ConfigureAwait(false);
        if (!File.Exists(catalogPath))
        {
            return Array.Empty<AiModelInfo>();
        }

        try
        {
            await using FileStream fileStream = File.OpenRead(catalogPath);
            List<AiModelCatalogEntry>? catalogEntries = await JsonSerializer.DeserializeAsync<List<AiModelCatalogEntry>>(
                fileStream,
                JsonSerializerOptions,
                cancellationToken).ConfigureAwait(false);

            if (catalogEntries is null)
            {
                return Array.Empty<AiModelInfo>();
            }

            return catalogEntries
                .Select(entry => MapToModelInfo(entry, catalogPath))
                .Select(ApplyInstallationState)
                .ToList();
        }
        catch (JsonException)
        {
            return Array.Empty<AiModelInfo>();
        }
    }

    public async Task<AiModelInfo?> GetModelAsync(string modelId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return null;
        }

        IReadOnlyList<AiModelInfo> availableModels = await GetAvailableModelsAsync(cancellationToken).ConfigureAwait(false);
        return availableModels.FirstOrDefault(model =>
            string.Equals(model.Id, modelId, StringComparison.OrdinalIgnoreCase));
    }

    private static AiModelInfo MapToModelInfo(AiModelCatalogEntry catalogEntry, string catalogPath)
    {
        bool hasLegacyModelPath = !string.IsNullOrWhiteSpace(catalogEntry.ModelPath);
        string repositoryId = string.IsNullOrWhiteSpace(catalogEntry.RepositoryId)
            ? (hasLegacyModelPath ? string.Empty : catalogEntry.Id)
            : catalogEntry.RepositoryId;
        string modelId = string.IsNullOrWhiteSpace(catalogEntry.Id)
            ? repositoryId
            : catalogEntry.Id;
        string modelDisplayName = string.IsNullOrWhiteSpace(catalogEntry.DisplayName)
            ? DeriveDisplayName(repositoryId, modelId)
            : catalogEntry.DisplayName;

        AiModelInfo model = new AiModelInfo
        {
            Id = modelId,
            DisplayName = modelDisplayName,
            RepositoryId = repositoryId,
            Revision = catalogEntry.Revision,
            IncludePatterns = catalogEntry.IncludePatterns.ToArray(),
            ExcludePatterns = catalogEntry.ExcludePatterns.ToArray(),
        };

        string resolvedLocalModelPath = ResolveLegacyModelPath(catalogEntry.ModelPath, catalogPath);
        if (!string.IsNullOrWhiteSpace(resolvedLocalModelPath))
        {
            model.ModelPath = resolvedLocalModelPath;
            model.IsInstalled = Directory.Exists(resolvedLocalModelPath) || File.Exists(resolvedLocalModelPath);
            return model;
        }

        if (!string.IsNullOrWhiteSpace(model.RepositoryId))
        {
            model.ModelPath = string.Empty;
            model.IsInstalled = false;
        }

        return model;
    }

    private AiModelInfo ApplyInstallationState(AiModelInfo model)
    {
        if (!string.IsNullOrWhiteSpace(model.RepositoryId))
        {
            bool isInstalled = huggingFaceCliService.IsModelInstalled(model);
            model.IsInstalled = isInstalled;
            if (isInstalled)
            {
                model.ModelPath = huggingFaceCliService.GetModelInstallDirectory(model);
            }
        }

        return model;
    }

    private async Task<string> ResolveAiModelsPathAsync()
    {
        AppState appState = await statePersistenceService.LoadAppStateAsync().ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(appState.LastMasterRootDirectory))
        {
            string overrideCatalogPath = Path.Combine(appState.LastMasterRootDirectory, "ai_models.json");
            if (File.Exists(overrideCatalogPath))
            {
                return overrideCatalogPath;
            }
        }

        return Path.Combine(AppContext.BaseDirectory, "ai_models.json");
    }

    private static string ResolveLegacyModelPath(string configuredModelPath, string catalogPath)
    {
        if (string.IsNullOrWhiteSpace(configuredModelPath))
        {
            return string.Empty;
        }

        if (Path.IsPathRooted(configuredModelPath))
        {
            return configuredModelPath;
        }

        string? catalogDirectoryPath = Path.GetDirectoryName(catalogPath);
        if (string.IsNullOrWhiteSpace(catalogDirectoryPath))
        {
            return configuredModelPath;
        }

        return Path.Combine(catalogDirectoryPath, configuredModelPath);
    }

    private static string DeriveDisplayName(string repositoryId, string modelId)
    {
        string sourceText = !string.IsNullOrWhiteSpace(repositoryId)
            ? repositoryId.Split('/').Last()
            : modelId;
        return sourceText.Replace('-', ' ').Replace('_', ' ');
    }
}
