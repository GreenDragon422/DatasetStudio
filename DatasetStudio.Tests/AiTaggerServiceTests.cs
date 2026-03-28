using CommunityToolkit.Mvvm.Messaging;
using DatasetStudio.Models;
using DatasetStudio.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DatasetStudio.Tests;

[TestFixture]
public class AiTaggerServiceTests
{
    [Test]
    public void GenerateTagsAsync_UninstalledHuggingFaceModel_DoesNotAutoDownload()
    {
        List<AiModelInfo> models = new List<AiModelInfo>
        {
            new AiModelInfo
            {
                Id = "wd14-vit",
                DisplayName = "WD14 ViT",
                RepositoryId = "SmilingWolf/wd-vit-large-tagger-v3",
                IsInstalled = false,
            },
        };
        FakeAiModelCatalogService aiModelCatalogService = new FakeAiModelCatalogService(models);
        FakeHuggingFaceCliService huggingFaceCliService = new FakeHuggingFaceCliService();
        StrongReferenceMessenger messenger = new StrongReferenceMessenger();
        AiTaggerService service = new AiTaggerService(
            aiModelCatalogService,
            huggingFaceCliService,
            messenger);

        InvalidOperationException exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.GenerateTagsAsync("C:\\images\\cat.png", "wd14-vit").ConfigureAwait(false));

        Assert.That(exception.Message, Does.Contain("Download Model button"));
        Assert.That(huggingFaceCliService.DownloadInvocationCount, Is.EqualTo(0));
    }

    private sealed class FakeAiModelCatalogService : IAiModelCatalogService
    {
        private readonly IReadOnlyList<AiModelInfo> models;

        public FakeAiModelCatalogService(IReadOnlyList<AiModelInfo> models)
        {
            this.models = models;
        }

        public Task<IReadOnlyList<AiModelInfo>> GetAvailableModelsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(models);
        }

        public Task<AiModelInfo?> GetModelAsync(string modelId, CancellationToken cancellationToken = default)
        {
            AiModelInfo? model = models.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, modelId, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(model);
        }
    }

    private sealed class FakeHuggingFaceCliService : IHuggingFaceCliService
    {
        public int DownloadInvocationCount { get; private set; }

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
            return string.Format("C:\\models\\{0}", model.Id);
        }

        public bool IsModelInstalled(AiModelInfo model)
        {
            return model.IsInstalled;
        }

        public Task<string> DownloadModelAsync(AiModelInfo model, CancellationToken cancellationToken = default)
        {
            DownloadInvocationCount += 1;
            return Task.FromResult(GetModelInstallDirectory(model));
        }
    }
}
