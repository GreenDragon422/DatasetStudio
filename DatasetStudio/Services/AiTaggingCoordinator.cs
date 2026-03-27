using CommunityToolkit.Mvvm.Messaging;
using DatasetStudio.Messages;
using System;
using System.Threading.Tasks;

namespace DatasetStudio.Services;

public sealed class AiTaggingCoordinator : IDisposable
{
    private readonly IAiTaggerService aiTaggerService;
    private readonly ITagFileService tagFileService;
    private readonly IMessenger messenger;
    private bool isDisposed;

    public AiTaggingCoordinator(
        IAiTaggerService aiTaggerService,
        ITagFileService tagFileService,
        IMessenger messenger)
    {
        this.aiTaggerService = aiTaggerService ?? throw new ArgumentNullException(nameof(aiTaggerService));
        this.tagFileService = tagFileService ?? throw new ArgumentNullException(nameof(tagFileService));
        this.messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));

        this.aiTaggerService.TagGenerationCompleted += OnTagGenerationCompleted;
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        aiTaggerService.TagGenerationCompleted -= OnTagGenerationCompleted;
        isDisposed = true;
    }

    private void OnTagGenerationCompleted(object? sender, AiTaggingCompletedMessage message)
    {
        _ = PersistAndPublishAsync(message);
    }

    private async Task PersistAndPublishAsync(AiTaggingCompletedMessage message)
    {
        try
        {
            string tagFilePath = tagFileService.GetTagFilePath(message.ImagePath);
            await tagFileService.WriteTagsAsync(tagFilePath, message.GeneratedTags).ConfigureAwait(false);
            messenger.Send(message);
        }
        catch
        {
        }
    }
}
