using CommunityToolkit.Mvvm.Messaging;
using DatasetStudio.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DatasetStudio.Services;

public sealed class BatchTagOperationService
{
    private readonly IMessenger messenger;
    private readonly ITagDictionaryService tagDictionaryService;
    private readonly ITagFileService tagFileService;

    public BatchTagOperationService(ITagFileService tagFileService, ITagDictionaryService tagDictionaryService, IMessenger messenger)
    {
        this.tagFileService = tagFileService ?? throw new ArgumentNullException(nameof(tagFileService));
        this.tagDictionaryService = tagDictionaryService ?? throw new ArgumentNullException(nameof(tagDictionaryService));
        this.messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
    }

    public async Task AddTagAsync(string projectId, IReadOnlyList<string> imageFilePaths, string tag)
    {
        string resolvedTag = tagDictionaryService.ResolveAlias(projectId, tag);

        foreach (string imageFilePath in imageFilePaths)
        {
            string tagFilePath = tagFileService.GetTagFilePath(imageFilePath);
            IReadOnlyList<string> existingTags = await tagFileService.ReadTagsAsync(tagFilePath).ConfigureAwait(false);

            if (existingTags.Any(existingTag => string.Equals(existingTag, resolvedTag, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            List<string> updatedTags = existingTags.ToList();
            updatedTags.Add(resolvedTag);

            await tagFileService.WriteTagsAsync(tagFilePath, updatedTags).ConfigureAwait(false);
            messenger.Send(new TagsChangedMessage(imageFilePath, updatedTags));
        }
    }

    public async Task RemoveTagAsync(string projectId, IReadOnlyList<string> imageFilePaths, string tag)
    {
        string resolvedTag = tagDictionaryService.ResolveAlias(projectId, tag);

        foreach (string imageFilePath in imageFilePaths)
        {
            string tagFilePath = tagFileService.GetTagFilePath(imageFilePath);
            IReadOnlyList<string> existingTags = await tagFileService.ReadTagsAsync(tagFilePath).ConfigureAwait(false);
            List<string> updatedTags = existingTags
                .Where(existingTag => !string.Equals(existingTag, tag, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(tagDictionaryService.ResolveAlias(projectId, existingTag), resolvedTag, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (updatedTags.Count == existingTags.Count)
            {
                continue;
            }

            await tagFileService.WriteTagsAsync(tagFilePath, updatedTags).ConfigureAwait(false);
            messenger.Send(new TagsChangedMessage(imageFilePath, updatedTags));
        }
    }
}
