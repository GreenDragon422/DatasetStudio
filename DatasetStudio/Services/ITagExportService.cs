using DatasetStudio.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DatasetStudio.Services;

public interface ITagExportService
{
    IReadOnlyList<string> BuildTrainingTags(ImageTaggingResult taggingResult);

    Task WriteTrainingSidecarAsync(
        string imageFilePath,
        ImageTaggingResult taggingResult,
        CancellationToken cancellationToken = default);
}
