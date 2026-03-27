using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DatasetStudio.Services;

public class TagFileService : ITagFileService
{
    public string GetTagFilePath(string imageFilePath)
    {
        return Path.ChangeExtension(imageFilePath, ".txt");
    }

    public bool TagFileExists(string imageFilePath)
    {
        string tagFilePath = GetTagFilePath(imageFilePath);
        return File.Exists(tagFilePath);
    }

    public async Task<IReadOnlyList<string>> ReadTagsAsync(string tagFilePath)
    {
        if (!File.Exists(tagFilePath))
        {
            return [];
        }

        string fileContents = await File.ReadAllTextAsync(tagFilePath).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(fileContents))
        {
            return [];
        }

        List<string> parsedTags = fileContents
            .Split(',')
            .Select(tag => tag.Trim())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .ToList();

        return parsedTags;
    }

    public async Task WriteTagsAsync(string tagFilePath, IReadOnlyList<string> tags)
    {
        List<string> sanitizedTags = tags
            .Select(tag => tag.Trim())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .ToList();

        string serializedTags = string.Join(", ", sanitizedTags);
        await File.WriteAllTextAsync(tagFilePath, serializedTags).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> ReadTagsWithPrefixAsync(string tagFilePath, IReadOnlyList<string> prefixTags)
    {
        IReadOnlyList<string> parsedTags = await ReadTagsAsync(tagFilePath).ConfigureAwait(false);

        List<string> combinedTags = prefixTags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Concat(parsedTags)
            .ToList();

        return combinedTags;
    }
}
