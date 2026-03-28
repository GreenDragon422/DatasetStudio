using DatasetStudio.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DatasetStudio.Services;

public sealed class TagSidecarService : ITagSidecarService
{
    private readonly ITagFileService tagFileService;

    public TagSidecarService(ITagFileService tagFileService)
    {
        this.tagFileService = tagFileService ?? throw new ArgumentNullException(nameof(tagFileService));
    }

    public IReadOnlyList<string> BuildTrainingTags(ImageTaggingResult taggingResult)
    {
        if (taggingResult is null)
        {
            throw new ArgumentNullException(nameof(taggingResult));
        }

        return taggingResult.AcceptedTrainingTags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task WriteTrainingSidecarAsync(
        string imageFilePath,
        ImageTaggingResult taggingResult,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string tagFilePath = tagFileService.GetTagFilePath(imageFilePath);
        IReadOnlyList<string> tags = BuildTrainingTags(taggingResult);
        await tagFileService.WriteTagsAsync(tagFilePath, tags).ConfigureAwait(false);
    }
}
