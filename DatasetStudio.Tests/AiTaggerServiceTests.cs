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
        FakeTaggerSession taggerSession = new FakeTaggerSession();
        StrongReferenceMessenger messenger = new StrongReferenceMessenger();
        AiTaggerService service = new AiTaggerService(
            aiModelCatalogService,
            huggingFaceCliService,
            taggerSession,
            messenger);

        InvalidOperationException exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.GenerateTagsAsync("C:\\images\\cat.png", "wd14-vit").ConfigureAwait(false));

        Assert.That(exception.Message, Does.Contain("Download Model button"));
        Assert.That(huggingFaceCliService.DownloadInvocationCount, Is.EqualTo(0));
    }

    [Test]
    public async Task GenerateTagsAsync_InstalledWdModel_UsesResolvedFilesAndReturnsTrainingTags()
    {
        string modelDirectoryPath = CreateInstalledWdModelDirectory(includeTagCsv: true);
        List<AiModelInfo> models = new List<AiModelInfo>
        {
            new AiModelInfo
            {
                Id = "wd-swinv2",
                DisplayName = "WD SwinV2 Tagger v3",
                RepositoryId = "SmilingWolf/wd-swinv2-tagger-v3",
                ModelPath = modelDirectoryPath,
                IsInstalled = true,
            },
        };
        FakeAiModelCatalogService aiModelCatalogService = new FakeAiModelCatalogService(models);
        FakeHuggingFaceCliService huggingFaceCliService = new FakeHuggingFaceCliService();
        FakeTaggerSession taggerSession = new FakeTaggerSession
        {
            Result = new ImageTaggingResult
            {
                AcceptedTrainingTags = new[] { "1girl", "blue eyes" },
            },
        };
        StrongReferenceMessenger messenger = new StrongReferenceMessenger();
        AiTaggerService service = new AiTaggerService(
            aiModelCatalogService,
            huggingFaceCliService,
            taggerSession,
            messenger);

        IReadOnlyList<string> tags = await service.GenerateTagsAsync("C:\\images\\cat.png", "wd-swinv2").ConfigureAwait(false);

        Assert.That(tags, Is.EqualTo(new[] { "1girl", "blue eyes" }));
        Assert.That(taggerSession.LastModelConfig?.ModelFilePath, Is.EqualTo(Path.Combine(modelDirectoryPath, "model.onnx")));
        Assert.That(taggerSession.LastModelConfig?.TagCsvPath, Is.EqualTo(Path.Combine(modelDirectoryPath, "selected_tags.csv")));
        Assert.That(taggerSession.LastModelConfig?.BatchSize, Is.EqualTo(32));
        Assert.That(taggerSession.LastModelConfig?.GeneralThreshold, Is.EqualTo(0.35f));
        Assert.That(taggerSession.LastModelConfig?.CharacterThreshold, Is.EqualTo(0.85f));
    }

    [Test]
    public void GenerateTagsAsync_InstalledModelMissingTagCsv_ThrowsHelpfulMessage()
    {
        string modelDirectoryPath = CreateInstalledWdModelDirectory(includeTagCsv: false);
        List<AiModelInfo> models = new List<AiModelInfo>
        {
            new AiModelInfo
            {
                Id = "wd-swinv2",
                DisplayName = "WD SwinV2 Tagger v3",
                RepositoryId = "SmilingWolf/wd-swinv2-tagger-v3",
                ModelPath = modelDirectoryPath,
                IsInstalled = true,
            },
        };
        FakeAiModelCatalogService aiModelCatalogService = new FakeAiModelCatalogService(models);
        FakeHuggingFaceCliService huggingFaceCliService = new FakeHuggingFaceCliService();
        FakeTaggerSession taggerSession = new FakeTaggerSession();
        StrongReferenceMessenger messenger = new StrongReferenceMessenger();
        AiTaggerService service = new AiTaggerService(
            aiModelCatalogService,
            huggingFaceCliService,
            taggerSession,
            messenger);

        InvalidOperationException exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.GenerateTagsAsync("C:\\images\\cat.png", "wd-swinv2").ConfigureAwait(false));

        Assert.That(exception.Message, Does.Contain("selected_tags.csv"));
        Assert.That(exception.Message, Does.Contain("WD-style ONNX taggers"));
    }

    private static string CreateInstalledWdModelDirectory(bool includeTagCsv)
    {
        string modelDirectoryPath = Path.Combine(
            Path.GetTempPath(),
            "DatasetStudioTests",
            Guid.NewGuid().ToString("N"),
            "wd-swinv2");
        Directory.CreateDirectory(modelDirectoryPath);
        File.WriteAllBytes(Path.Combine(modelDirectoryPath, "model.onnx"), new byte[] { 1, 2, 3 });

        if (includeTagCsv)
        {
            File.WriteAllLines(
                Path.Combine(modelDirectoryPath, "selected_tags.csv"),
                new[]
                {
                    "tag_id,name,category,count",
                    "0,safe,9,1",
                });
        }

        return modelDirectoryPath;
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

    private sealed class FakeTaggerSession : ITaggerSession
    {
        public ImageTaggingResult Result { get; set; } = new ImageTaggingResult();

        public TaggerModelConfig? LastModelConfig { get; private set; }

        public Task<ImageTaggingResult> TagImageAsync(
            TaggerModelConfig modelConfig,
            string imageFilePath,
            CancellationToken cancellationToken = default)
        {
            LastModelConfig = modelConfig;
            return Task.FromResult(Result);
        }
    }
}
