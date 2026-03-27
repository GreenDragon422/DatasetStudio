using System.Collections.Generic;
using System.Threading.Tasks;

namespace DatasetStudio.Services;

public interface ITagFileService
{
    Task<IReadOnlyList<string>> ReadTagsAsync(string tagFilePath);

    Task WriteTagsAsync(string tagFilePath, IReadOnlyList<string> tags);

    Task<IReadOnlyList<string>> ReadTagsWithPrefixAsync(string tagFilePath, IReadOnlyList<string> prefixTags);

    string GetTagFilePath(string imageFilePath);

    bool TagFileExists(string imageFilePath);
}
