using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DatasetStudio.Services;

namespace DatasetStudio.Tests.TestDoubles;

public sealed class RecordingClipboardService : IClipboardService
{
    public IReadOnlyList<string> LastCopiedTags { get; private set; } = [];

    public IReadOnlyList<string> PastedTags { get; set; } = [];

    public Task CopyTagsAsync(IReadOnlyList<string> tags)
    {
        LastCopiedTags = tags.ToList();
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> PasteTagsAsync()
    {
        return Task.FromResult(PastedTags);
    }
}
