using DatasetStudio.Models;
using System.Threading;
using System.Threading.Tasks;

namespace DatasetStudio.Services;

public interface ITaggerSession
{
    Task<ImageTaggingResult> TagImageAsync(
        TaggerModelConfig modelConfig,
        string imageFilePath,
        CancellationToken cancellationToken = default);
}
