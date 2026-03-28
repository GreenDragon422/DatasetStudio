using CommunityToolkit.Mvvm.Messaging;
using DatasetStudio.Messages;
using System;
using System.Threading.Tasks;

namespace DatasetStudio.Services;

public sealed class AiTaggingCoordinator : IDisposable
{
    private readonly IAiTaggerService aiTaggerService;
    private readonly ITagExportService tagExportService;
    private readonly IMessenger messenger;
    private bool isDisposed;

    public AiTaggingCoordinator(
        IAiTaggerService aiTaggerService,
        ITagExportService tagExportService,
        IMessenger messenger)
    {
        this.aiTaggerService = aiTaggerService ?? throw new ArgumentNullException(nameof(aiTaggerService));
        this.tagExportService = tagExportService ?? throw new ArgumentNullException(nameof(tagExportService));
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
            await tagExportService.WriteTrainingSidecarAsync(message.ImagePath, message.Result).ConfigureAwait(false);
            messenger.Send(message);
        }
        catch
        {
        }
    }
}
