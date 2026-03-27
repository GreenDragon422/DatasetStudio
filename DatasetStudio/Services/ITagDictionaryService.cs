using System.Collections.Generic;
using System.Threading.Tasks;
using DatasetStudio.Models;

namespace DatasetStudio.Services;

public interface ITagDictionaryService
{
    Task<IReadOnlyList<TagDictionaryEntry>> GetAllEntriesAsync(string projectId);

    Task<IReadOnlyList<string>> SearchTagsAsync(string projectId, string query);

    Task RenameTagAsync(string projectId, string oldName, string newName);

    Task MergeTagsAsync(string projectId, string sourceTag, string targetTag);

    Task DeleteTagAsync(string projectId, string tagName, bool removeFromFiles);

    Task AddAliasAsync(string projectId, string canonicalTag, string alias);

    Task SetAliasesAsync(string projectId, string canonicalTag, IReadOnlyList<string> aliases);

    string ResolveAlias(string projectId, string input);
}
