using DatasetStudio.Models;
using DatasetStudio.Services;
using DatasetStudio.Tests.TestDoubles;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DatasetStudio.Tests;

[TestFixture]
public class AiModelCatalogServiceTests
{
    [Test]
    public async Task GetAvailableModelsAsync_UsesMasterRootCatalogAndAppliesInstallationState()
    {
        string tempRootDirectoryPath = Path.Combine(Path.GetTempPath(), "DatasetStudioTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRootDirectoryPath);

        List<AiModelCatalogEntry> catalogEntries = new List<AiModelCatalogEntry>
        {
            new AiModelCatalogEntry
            {
                RepositoryId = "SmilingWolf/wd-eva02-large-tagger-v3",
            },
            new AiModelCatalogEntry
            {
                Id = "legacy-local-model",
                DisplayName = "Legacy Local Model",
                ModelPath = "models\\legacy-local-model",
            },
        };
        string catalogPath = Path.Combine(tempRootDirectoryPath, "ai_models.json");
        string legacyModelDirectoryPath = Path.Combine(tempRootDirectoryPath, "models", "legacy-local-model");
        Directory.CreateDirectory(legacyModelDirectoryPath);
        File.WriteAllText(
            catalogPath,
            JsonSerializer.Serialize(catalogEntries, new JsonSerializerOptions { WriteIndented = true }));

        TestStatePersistenceService statePersistenceService = new TestStatePersistenceService();
        AppState appState = statePersistenceService.GetAppState();
        appState.LastMasterRootDirectory = tempRootDirectoryPath;
        statePersistenceService.SetAppState(appState);
        FakeHuggingFaceCliService huggingFaceCliService = new FakeHuggingFaceCliService(tempRootDirectoryPath);
        huggingFaceCliService.InstalledRepositoryIds.Add("SmilingWolf/wd-eva02-large-tagger-v3");

        AiModelCatalogService service = new AiModelCatalogService(statePersistenceService, huggingFaceCliService);

        IReadOnlyList<AiModelInfo> models = await service.GetAvailableModelsAsync().ConfigureAwait(false);

        Assert.That(models.Count, Is.EqualTo(2));

        AiModelInfo huggingFaceModel = models[0];
        Assert.That(huggingFaceModel.Id, Is.EqualTo("SmilingWolf/wd-eva02-large-tagger-v3"));
        Assert.That(huggingFaceModel.IsInstalled, Is.True);
        Assert.That(huggingFaceModel.ModelPath, Is.EqualTo(Path.Combine(tempRootDirectoryPath, "hf-models", "SmilingWolf", "wd-eva02-large-tagger-v3")));

        AiModelInfo localModel = models[1];
        Assert.That(localModel.Id, Is.EqualTo("legacy-local-model"));
        Assert.That(localModel.IsInstalled, Is.True);
        Assert.That(localModel.ModelPath, Is.EqualTo(legacyModelDirectoryPath));
    }

    private sealed class FakeHuggingFaceCliService : IHuggingFaceCliService
    {
        private readonly string tempRootDirectoryPath;

        public FakeHuggingFaceCliService(string tempRootDirectoryPath)
        {
            this.tempRootDirectoryPath = tempRootDirectoryPath;
            InstalledRepositoryIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        public HashSet<string> InstalledRepositoryIds { get; }

        public Task<HuggingFaceCliStatus> EnsureCliAvailableAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new HuggingFaceCliStatus
            {
                IsAvailable = true,
                ExecutablePath = "hf",
                StatusMessage = "available",
            });
        }

        public string GetModelInstallDirectory(AiModelInfo model)
        {
            return Path.Combine(tempRootDirectoryPath, "hf-models", model.RepositoryId.Replace('/', Path.DirectorySeparatorChar));
        }

        public bool IsModelInstalled(AiModelInfo model)
        {
            return InstalledRepositoryIds.Contains(model.RepositoryId);
        }

        public Task<string> DownloadModelAsync(AiModelInfo model, CancellationToken cancellationToken = default)
        {
            InstalledRepositoryIds.Add(model.RepositoryId);
            return Task.FromResult(GetModelInstallDirectory(model));
        }
    }
}
