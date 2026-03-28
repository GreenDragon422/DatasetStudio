using CommunityToolkit.Mvvm.Messaging;
using DatasetStudio.Models;
using DatasetStudio.Services;
using DatasetStudio.ViewModels;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DatasetStudio.Tests;

[TestFixture]
public class TagsOverviewViewModelTests
{
    private sealed class FakeTagDictionaryService : ITagDictionaryService
    {
        public IReadOnlyList<TagDictionaryEntry> Entries { get; set; } = Array.Empty<TagDictionaryEntry>();

        public Task<IReadOnlyList<string>> SearchTagsAsync(string projectId, string query)
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        public Task<IReadOnlyList<TagDictionaryEntry>> GetAllEntriesAsync(string projectId)
        {
            return Task.FromResult(Entries);
        }

        public Task RenameTagAsync(string projectId, string oldName, string newName)
        {
            return Task.CompletedTask;
        }

        public Task MergeTagsAsync(string projectId, string sourceTag, string targetTag)
        {
            return Task.CompletedTask;
        }

        public Task DeleteTagAsync(string projectId, string tagName, bool removeFromFiles)
        {
            return Task.CompletedTask;
        }

        public Task AddAliasAsync(string projectId, string canonicalTag, string alias)
        {
            return Task.CompletedTask;
        }

        public Task SetAliasesAsync(string projectId, string canonicalTag, IReadOnlyList<string> aliases)
        {
            return Task.CompletedTask;
        }

        public string ResolveAlias(string projectId, string input)
        {
            return input;
        }
    }

    [Test]
    public async Task SearchText_FiltersEntriesByCanonicalNameAndAlias()
    {
        FakeTagDictionaryService tagDictionaryService = new FakeTagDictionaryService
        {
            Entries = new List<TagDictionaryEntry>
            {
                new TagDictionaryEntry
                {
                    CanonicalName = "cat",
                    Aliases = new List<string> { "kitty" },
                    GlobalFrequency = 9,
                },
                new TagDictionaryEntry
                {
                    CanonicalName = "dog",
                    Aliases = new List<string> { "puppy" },
                    GlobalFrequency = 4,
                },
            },
        };
        TagsOverviewViewModel viewModel = new TagsOverviewViewModel(tagDictionaryService, new WeakReferenceMessenger());

        viewModel.OnNavigatedTo("project-1");
        await WaitForConditionAsync(() => viewModel.AllEntries.Count == 2).ConfigureAwait(false);

        viewModel.SearchText = "kit";

        Assert.That(viewModel.FilteredEntries.Count, Is.EqualTo(1));
        Assert.That(viewModel.FilteredEntries[0].CanonicalName, Is.EqualTo("cat"));

        viewModel.SearchText = "dog";

        Assert.That(viewModel.FilteredEntries.Count, Is.EqualTo(1));
        Assert.That(viewModel.FilteredEntries[0].CanonicalName, Is.EqualTo("dog"));
    }

    private static async Task WaitForConditionAsync(Func<bool> condition)
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(10).ConfigureAwait(false);
        }

        Assert.Fail("Condition was not met within the expected time.");
    }
}
