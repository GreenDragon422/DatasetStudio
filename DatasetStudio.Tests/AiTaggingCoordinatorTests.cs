using CommunityToolkit.Mvvm.Messaging;
using DatasetStudio.Messages;
using DatasetStudio.Services;
using DatasetStudio.Tests.TestDoubles;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace DatasetStudio.Tests;

[TestFixture]
public class AiTaggingCoordinatorTests
{
    [Test]
    public async Task CompletedGeneration_PersistsTagFileAndPublishesMessage()
    {
        StrongReferenceMessenger messenger = new StrongReferenceMessenger();
        TagFileService tagFileService = new TagFileService();
        TestAiTaggerService aiTaggerService = new TestAiTaggerService();
        AiTaggingCoordinator coordinator = new AiTaggingCoordinator(aiTaggerService, tagFileService, messenger);

        string imageFilePath = Path.Combine(Path.GetTempPath(), "DatasetStudioAiCoordinator", "cat.png");
        Directory.CreateDirectory(Path.GetDirectoryName(imageFilePath)!);
        await File.WriteAllBytesAsync(imageFilePath, new byte[] { 1, 2, 3 }).ConfigureAwait(false);

        AiTaggingCompletedMessage? publishedMessage = null;
        MessageRecorder messageRecorder = new MessageRecorder();
        messenger.Register<MessageRecorder, AiTaggingCompletedMessage>(messageRecorder, static (recipient, message) =>
        {
            recipient.LastMessage = message;
        });

        aiTaggerService.Complete(imageFilePath, new[] { "cat", "studio" });

        await WaitForConditionAsync(async () =>
        {
            IReadOnlyList<string> persistedTags = await tagFileService.ReadTagsAsync(tagFileService.GetTagFilePath(imageFilePath)).ConfigureAwait(false);
            publishedMessage = messageRecorder.LastMessage;
            return persistedTags.Count == 2 && publishedMessage is not null;
        }).ConfigureAwait(false);

        IReadOnlyList<string> finalTags = await tagFileService.ReadTagsAsync(tagFileService.GetTagFilePath(imageFilePath)).ConfigureAwait(false);
        Assert.That(finalTags, Is.EqualTo(new[] { "cat", "studio" }));
        Assert.That(publishedMessage?.ImagePath, Is.EqualTo(imageFilePath));

        coordinator.Dispose();
    }

    private sealed class MessageRecorder
    {
        public AiTaggingCompletedMessage? LastMessage { get; set; }
    }

    private static async Task WaitForConditionAsync(Func<Task<bool>> condition)
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            if (await condition().ConfigureAwait(false))
            {
                return;
            }

            await Task.Delay(10).ConfigureAwait(false);
        }

        Assert.Fail("Condition was not met within the allotted time.");
    }
}
