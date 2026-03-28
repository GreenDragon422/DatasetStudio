using DatasetStudio.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DatasetStudio.Services;

public interface IAiModelCatalogService
{
    Task<IReadOnlyList<AiModelInfo>> GetAvailableModelsAsync(CancellationToken cancellationToken = default);

    Task<AiModelInfo?> GetModelAsync(string modelId, CancellationToken cancellationToken = default);
}
