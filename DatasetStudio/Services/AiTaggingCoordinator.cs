using CommunityToolkit.Mvvm.Messaging;
using DatasetStudio.Messages;
using System;
using System.Threading.Tasks;

namespace DatasetStudio.Services;

public sealed class AiTaggingCoordinator : IDisposable
{
    private readonly IAiTaggerService aiTaggerService;
    private readonly ITagSidecarService tagSidecarService;
    private readonly IMessenger messenger;
    private bool isDisposed;

    public AiTaggingCoordinator(
        IAiTaggerService aiTaggerService,
        ITagSidecarService tagSidecarService,
        IMessenger messenger)
    {
        this.aiTaggerService = aiTaggerService ?? throw new ArgumentNullException(nameof(aiTaggerService));
        this.tagSidecarService = tagSidecarService ?? throw new ArgumentNullException(nameof(tagSidecarService));
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
            await tagSidecarService.WriteTrainingSidecarAsync(message.ImagePath, message.Result).ConfigureAwait(false);
            messenger.Send(message);
        }
        catch
        {
        }
    }
}
