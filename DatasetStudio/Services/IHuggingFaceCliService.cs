using DatasetStudio.Models;
using System.Threading;
using System.Threading.Tasks;

namespace DatasetStudio.Services;

public interface IHuggingFaceCliService
{
    Task<HuggingFaceCliStatus> EnsureCliAvailableAsync(CancellationToken cancellationToken = default);

    string GetModelInstallDirectory(AiModelInfo model);

    bool IsModelInstalled(AiModelInfo model);

    Task<string> DownloadModelAsync(AiModelInfo model, CancellationToken cancellationToken = default);
}
