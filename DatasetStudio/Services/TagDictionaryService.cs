using CommunityToolkit.Mvvm.Messaging;
using DatasetStudio.Messages;
using DatasetStudio.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace DatasetStudio.Services;

public sealed class TagDictionaryService : ITagDictionaryService
{
    private static readonly string[] SupportedImageExtensions =
    [
        ".png",
        ".jpg",
        ".jpeg",
        ".webp",
        ".bmp",
    ];

    private readonly IProjectService projectService;
    private readonly ITagFileService tagFileService;
    private readonly Dictionary<string, List<TagDictionaryEntry>> dictionaryEntriesCacheByProjectId;
    private readonly Dictionary<string, string> projectRootPathByProjectId;
    private readonly IMessenger messenger;
    private readonly object cacheSync;

    public TagDictionaryService(IProjectService projectService, ITagFileService tagFileService, IMessenger messenger)
    {
        this.projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
        this.tagFileService = tagFileService ?? throw new ArgumentNullException(nameof(tagFileService));

        if (messenger is null)
        {
            throw new ArgumentNullException(nameof(messenger));
        }

        this.messenger = messenger;
        dictionaryEntriesCacheByProjectId = new Dictionary<string, List<TagDictionaryEntry>>(StringComparer.OrdinalIgnoreCase);
        projectRootPathByProjectId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        cacheSync = new object();
        messenger.Register<TagDictionaryChangedMessage>(this, static (recipient, message) =>
        {
            TagDictionaryService service = (TagDictionaryService)recipient;
            service.InvalidateCachedProject(message.ProjectId);
        });
        messenger.Register<TagsChangedMessage>(this, static (recipient, message) =>
        {
            TagDictionaryService service = (TagDictionaryService)recipient;
            service.InvalidateCachedProjectForImage(message.ImagePath);
        });
        messenger.Register<AiTaggingCompletedMessage>(this, static (recipient, message) =>
        {
            TagDictionaryService service = (TagDictionaryService)recipient;
            service.InvalidateCachedProjectForImage(message.ImagePath);
        });
        messenger.Register<ImageDeletedMessage>(this, static (recipient, message) =>
        {
            TagDictionaryService service = (TagDictionaryService)recipient;
            service.InvalidateCachedProjectForImage(message.ImagePath);
        });
        messenger.Register<TagFilesChangedMessage>(this, static (recipient, message) =>
        {
            TagDictionaryService service = (TagDictionaryService)recipient;
            service.InvalidateCachedProjectForImage(message.FilePath);
        });
    }

    public async Task<IReadOnlyList<TagDictionaryEntry>> GetAllEntriesAsync(string projectId)
    {
        List<TagDictionaryEntry> entries = await GetCachedEntriesAsync(projectId);
        return entries
            .Select(CloneTagDictionaryEntry)
            .ToList();
    }

    public async Task<IReadOnlyList<string>> SearchTagsAsync(string projectId, string query)
    {
        List<TagDictionaryEntry> entries = await GetCachedEntriesAsync(projectId);

        if (string.IsNullOrWhiteSpace(query))
        {
            return entries
                .Select(entry => entry.CanonicalName)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        string normalizedQuery = query.Trim();

        return entries
            .Where(entry =>
                entry.CanonicalName.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                || entry.Aliases.Any(alias => alias.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)))
            .Select(entry => entry.CanonicalName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task RenameTagAsync(string projectId, string oldName, string newName)
    {
        string sanitizedOldName = SanitizeTagName(oldName, nameof(oldName));
        string sanitizedNewName = SanitizeTagName(newName, nameof(newName));
        List<TagDictionaryEntry> entries = await GetCachedEntriesAsync(projectId);
        TagDictionaryEntry sourceEntry = GetOrCreateEntry(entries, ResolveAliasUsingEntries(entries, sanitizedOldName));

        if (!string.Equals(sourceEntry.CanonicalName, sanitizedNewName, StringComparison.OrdinalIgnoreCase)
            && TryFindEntry(entries, sanitizedNewName, out TagDictionaryEntry? conflictingEntry)
            && !string.Equals(conflictingEntry.CanonicalName, sourceEntry.CanonicalName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cannot rename a tag to an existing canonical tag.");
        }

        List<string> tagsToReplace = new List<string>(sourceEntry.Aliases)
        {
            sourceEntry.CanonicalName,
        };

        await ReplaceTagsInProjectFilesAsync(projectId, tagsToReplace, sanitizedNewName, removeReplacementTag: false);

        string previousCanonicalName = sourceEntry.CanonicalName;
        sourceEntry.CanonicalName = sanitizedNewName;
        AddAliasIfMissing(sourceEntry, previousCanonicalName);
        RemoveSelfAliases(sourceEntry);

        await PersistEntriesAsync(projectId, entries);
    }

    public async Task MergeTagsAsync(string projectId, string sourceTag, string targetTag)
    {
        string sanitizedSourceTag = SanitizeTagName(sourceTag, nameof(sourceTag));
        string sanitizedTargetTag = SanitizeTagName(targetTag, nameof(targetTag));
        List<TagDictionaryEntry> entries = await GetCachedEntriesAsync(projectId);
        string sourceCanonicalName = ResolveAliasUsingEntries(entries, sanitizedSourceTag);
        string resolvedTargetTag = ResolveAliasUsingEntries(entries, sanitizedTargetTag);

        if (!string.Equals(resolvedTargetTag, sanitizedTargetTag, StringComparison.OrdinalIgnoreCase)
            && string.Equals(resolvedTargetTag, sourceCanonicalName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Merge would create a circular alias.");
        }

        if (string.Equals(sourceCanonicalName, resolvedTargetTag, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Merge would create a circular alias.");
        }

        TagDictionaryEntry sourceEntry = GetOrCreateEntry(entries, sourceCanonicalName);
        TagDictionaryEntry targetEntry = GetOrCreateEntry(entries, resolvedTargetTag);

        List<string> tagsToReplace = new List<string>(sourceEntry.Aliases)
        {
            sourceEntry.CanonicalName,
        };

        await ReplaceTagsInProjectFilesAsync(projectId, tagsToReplace, targetEntry.CanonicalName, removeReplacementTag: false);

        AddAliasIfMissing(targetEntry, sourceEntry.CanonicalName);

        foreach (string alias in sourceEntry.Aliases)
        {
            AddAliasIfMissing(targetEntry, alias);
        }

        RemoveSelfAliases(targetEntry);
        entries.RemoveAll(entry => string.Equals(entry.CanonicalName, sourceEntry.CanonicalName, StringComparison.OrdinalIgnoreCase));
        ValidateNoCircularAliases(entries);

        await PersistEntriesAsync(projectId, entries);
    }

    public async Task DeleteTagAsync(string projectId, string tagName, bool removeFromFiles)
    {
        string sanitizedTagName = SanitizeTagName(tagName, nameof(tagName));
        List<TagDictionaryEntry> entries = await GetCachedEntriesAsync(projectId);
        string canonicalName = ResolveAliasUsingEntries(entries, sanitizedTagName);
        TagDictionaryEntry entry = GetOrCreateEntry(entries, canonicalName);
        List<string> tagsToRemove = new List<string>(entry.Aliases)
        {
            entry.CanonicalName,
        };

        if (removeFromFiles)
        {
            await ReplaceTagsInProjectFilesAsync(projectId, tagsToRemove, replacementTag: null, removeReplacementTag: true);
        }

        entries.RemoveAll(existingEntry => string.Equals(existingEntry.CanonicalName, entry.CanonicalName, StringComparison.OrdinalIgnoreCase));
        await PersistEntriesAsync(projectId, entries);
    }

    public async Task AddAliasAsync(string projectId, string canonicalTag, string alias)
    {
        string sanitizedCanonicalTag = SanitizeTagName(canonicalTag, nameof(canonicalTag));
        string sanitizedAlias = SanitizeTagName(alias, nameof(alias));
        List<TagDictionaryEntry> entries = await GetCachedEntriesAsync(projectId);
        TagDictionaryEntry canonicalEntry = GetOrCreateEntry(entries, ResolveAliasUsingEntries(entries, sanitizedCanonicalTag));

        if (string.Equals(canonicalEntry.CanonicalName, sanitizedAlias, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string resolvedAlias = ResolveAliasUsingEntries(entries, sanitizedAlias);

        if (!string.Equals(resolvedAlias, sanitizedAlias, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(resolvedAlias, canonicalEntry.CanonicalName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Alias already belongs to another canonical tag.");
        }

        if (entries.Any(entry => !string.Equals(entry.CanonicalName, canonicalEntry.CanonicalName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(entry.CanonicalName, sanitizedAlias, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Alias cannot match another canonical tag.");
        }

        AddAliasIfMissing(canonicalEntry, sanitizedAlias);
        RemoveSelfAliases(canonicalEntry);
        ValidateNoCircularAliases(entries);

        await PersistEntriesAsync(projectId, entries);
    }

    public async Task SetAliasesAsync(string projectId, string canonicalTag, IReadOnlyList<string> aliases)
    {
        string sanitizedCanonicalTag = SanitizeTagName(canonicalTag, nameof(canonicalTag));
        List<TagDictionaryEntry> entries = await GetCachedEntriesAsync(projectId);
        TagDictionaryEntry entry = GetOrCreateEntry(entries, ResolveAliasUsingEntries(entries, sanitizedCanonicalTag));
        List<string> sanitizedAliases = aliases
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .Select(alias => alias.Trim())
            .Where(alias => !string.Equals(alias, entry.CanonicalName, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(alias => alias, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (string alias in sanitizedAliases)
        {
            if (entries.Any(otherEntry =>
                !string.Equals(otherEntry.CanonicalName, entry.CanonicalName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(otherEntry.CanonicalName, alias, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("Alias cannot match another canonical tag.");
            }

            string resolvedAlias = ResolveAliasUsingEntries(entries, alias);

            if (!string.Equals(resolvedAlias, alias, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(resolvedAlias, entry.CanonicalName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Alias already belongs to another canonical tag.");
            }
        }

        entry.Aliases = sanitizedAliases;
        ValidateNoCircularAliases(entries);
        await PersistEntriesAsync(projectId, entries);
    }

    public string ResolveAlias(string projectId, string input)
    {
        lock (cacheSync)
        {
            if (!dictionaryEntriesCacheByProjectId.TryGetValue(projectId, out List<TagDictionaryEntry>? entries))
            {
                return input;
            }

            return ResolveAliasUsingEntries(entries, input);
        }
    }

    private async Task<List<TagDictionaryEntry>> GetCachedEntriesAsync(string projectId)
    {
        lock (cacheSync)
        {
            if (dictionaryEntriesCacheByProjectId.TryGetValue(projectId, out List<TagDictionaryEntry>? cachedEntries))
            {
                return cachedEntries;
            }
        }

        Project project = await LoadProjectByIdAsync(projectId);
        lock (cacheSync)
        {
            projectRootPathByProjectId[projectId] = project.RootFolderPath;
        }

        List<string> tagFilePaths = EnumerateProjectTagFiles(project.RootFolderPath).ToList();
        string tagFilesFingerprint = BuildTagFilesFingerprint(tagFilePaths);
        List<TagDictionaryEntry> persistedEntries = project.TagDictionaryEntries
            .Select(CloneTagDictionaryEntry)
            .ToList();

        if (string.Equals(project.State?.TagStatisticsCacheFingerprint, tagFilesFingerprint, StringComparison.Ordinal))
        {
            List<TagDictionaryEntry> persistedSnapshotEntries = persistedEntries
                .OrderBy(entry => entry.CanonicalName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            lock (cacheSync)
            {
                dictionaryEntriesCacheByProjectId[projectId] = persistedSnapshotEntries;
            }

            return persistedSnapshotEntries;
        }

        Dictionary<string, int> frequencyByCanonicalName = await BuildFrequencyMapAsync(tagFilePaths, persistedEntries);
        Dictionary<string, TagDictionaryEntry> entriesByCanonicalName = persistedEntries
            .GroupBy(entry => entry.CanonicalName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.First().CanonicalName, group => CloneTagDictionaryEntry(group.First()), StringComparer.OrdinalIgnoreCase);

        foreach (KeyValuePair<string, int> frequencyEntry in frequencyByCanonicalName)
        {
            if (!entriesByCanonicalName.TryGetValue(frequencyEntry.Key, out TagDictionaryEntry? existingEntry))
            {
                existingEntry = new TagDictionaryEntry
                {
                    CanonicalName = frequencyEntry.Key,
                    Aliases = new List<string>(),
                    GlobalFrequency = 0,
                };

                entriesByCanonicalName[frequencyEntry.Key] = existingEntry;
            }

            existingEntry.GlobalFrequency = frequencyEntry.Value;
        }

        foreach (TagDictionaryEntry entry in entriesByCanonicalName.Values)
        {
            if (!frequencyByCanonicalName.TryGetValue(entry.CanonicalName, out int frequency))
            {
                entry.GlobalFrequency = 0;
            }

            entry.Aliases = entry.Aliases
                .Where(alias => !string.IsNullOrWhiteSpace(alias))
                .Select(alias => alias.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(alias => alias, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        List<TagDictionaryEntry> entries = entriesByCanonicalName.Values
            .OrderBy(entry => entry.CanonicalName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        await PersistTagStatisticsSnapshotAsync(project, entries, tagFilesFingerprint);
        lock (cacheSync)
        {
            dictionaryEntriesCacheByProjectId[projectId] = entries;
        }

        return entries;
    }

    private async Task<Dictionary<string, int>> BuildFrequencyMapAsync(IEnumerable<string> tagFilePaths, IReadOnlyList<TagDictionaryEntry> persistedEntries)
    {
        Dictionary<string, int> frequencyByCanonicalName = new(StringComparer.OrdinalIgnoreCase);

        foreach (string tagFilePath in tagFilePaths)
        {
            IReadOnlyList<string> tags = await tagFileService.ReadTagsAsync(tagFilePath);

            foreach (string tag in tags)
            {
                string canonicalTag = ResolveAliasUsingEntries(persistedEntries, tag);

                if (frequencyByCanonicalName.TryGetValue(canonicalTag, out int frequency))
                {
                    frequencyByCanonicalName[canonicalTag] = frequency + 1;
                }
                else
                {
                    frequencyByCanonicalName[canonicalTag] = 1;
                }
            }
        }

        return frequencyByCanonicalName;
    }

    private async Task ReplaceTagsInProjectFilesAsync(string projectId, IReadOnlyList<string> tagsToReplace, string? replacementTag, bool removeReplacementTag)
    {
        Project project = await LoadProjectByIdAsync(projectId);
        HashSet<string> tagsToReplaceSet = new(tagsToReplace, StringComparer.OrdinalIgnoreCase);

        foreach (string tagFilePath in EnumerateProjectTagFiles(project.RootFolderPath))
        {
            IReadOnlyList<string> tags = await tagFileService.ReadTagsAsync(tagFilePath);
            bool fileChanged = false;
            List<string> updatedTags = new();

            foreach (string tag in tags)
            {
                if (tagsToReplaceSet.Contains(tag))
                {
                    fileChanged = true;

                    if (!removeReplacementTag && !string.IsNullOrWhiteSpace(replacementTag))
                    {
                        updatedTags.Add(replacementTag);
                    }
                }
                else
                {
                    updatedTags.Add(tag);
                }
            }

            if (!fileChanged)
            {
                continue;
            }

            List<string> deduplicatedTags = updatedTags
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Select(tag => tag.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            await tagFileService.WriteTagsAsync(tagFilePath, deduplicatedTags);
        }
    }

    private async Task PersistEntriesAsync(string projectId, IReadOnlyList<TagDictionaryEntry> entries)
    {
        Project project = await LoadProjectByIdAsync(projectId);
        lock (cacheSync)
        {
            projectRootPathByProjectId[projectId] = project.RootFolderPath;
        }

        List<string> tagFilePaths = EnumerateProjectTagFiles(project.RootFolderPath).ToList();
        List<TagDictionaryEntry> persistedEntries = entries
            .Select(entry => new TagDictionaryEntry
            {
                CanonicalName = entry.CanonicalName,
                Aliases = entry.Aliases
                    .Where(alias => !string.IsNullOrWhiteSpace(alias))
                    .Select(alias => alias.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(alias => alias, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                GlobalFrequency = entry.GlobalFrequency,
            })
            .OrderBy(entry => entry.CanonicalName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        project.TagDictionaryEntries = persistedEntries
            .Select(CloneTagDictionaryEntry)
            .ToList();
        project.State ??= new ProjectState();
        project.State.TagStatisticsCacheFingerprint = BuildTagFilesFingerprint(tagFilePaths);

        await projectService.SaveProjectAsync(project);
        lock (cacheSync)
        {
            dictionaryEntriesCacheByProjectId[projectId] = persistedEntries;
        }
    }

    private async Task<Project> LoadProjectByIdAsync(string projectId)
    {
        IReadOnlyList<Project> projects = await projectService.LoadProjectsAsync();
        Project? project = projects.FirstOrDefault(candidate => string.Equals(candidate.Id, projectId, StringComparison.OrdinalIgnoreCase));

        if (project is null)
        {
            throw new InvalidOperationException($"Project '{projectId}' could not be found.");
        }

        return project;
    }

    private static IEnumerable<string> EnumerateProjectTagFiles(string projectRootPath)
    {
        if (!Directory.Exists(projectRootPath))
        {
            return [];
        }

        return Directory.EnumerateFiles(projectRootPath, "*.txt", SearchOption.AllDirectories)
            .Where(tagFilePath => !tagFilePath.Contains($"{Path.DirectorySeparatorChar}.datasetstudio-cache{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(HasCompanionImageFile)
            .OrderBy(tagFilePath => tagFilePath, StringComparer.OrdinalIgnoreCase);
    }

    private static bool HasCompanionImageFile(string tagFilePath)
    {
        string imageFilePathWithoutExtension = Path.Combine(
            Path.GetDirectoryName(tagFilePath) ?? string.Empty,
            Path.GetFileNameWithoutExtension(tagFilePath));

        foreach (string extension in SupportedImageExtensions)
        {
            if (File.Exists(imageFilePathWithoutExtension + extension))
            {
                return true;
            }
        }

        return false;
    }

    private static string ResolveAliasUsingEntries(IReadOnlyList<TagDictionaryEntry> entries, string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        string sanitizedInput = input.Trim();
        Dictionary<string, string> canonicalNameByAlias = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> canonicalNames = new(StringComparer.OrdinalIgnoreCase);

        foreach (TagDictionaryEntry entry in entries)
        {
            canonicalNames.Add(entry.CanonicalName);

            foreach (string alias in entry.Aliases)
            {
                canonicalNameByAlias[alias] = entry.CanonicalName;
            }
        }

        if (canonicalNames.Contains(sanitizedInput))
        {
            return entries.First(entry => string.Equals(entry.CanonicalName, sanitizedInput, StringComparison.OrdinalIgnoreCase)).CanonicalName;
        }

        string currentName = sanitizedInput;
        HashSet<string> visitedAliases = new(StringComparer.OrdinalIgnoreCase);

        while (canonicalNameByAlias.TryGetValue(currentName, out string? canonicalName))
        {
            if (!visitedAliases.Add(currentName))
            {
                throw new InvalidOperationException("Circular alias detected.");
            }

            currentName = canonicalName;
        }

        return currentName;
    }

    private static TagDictionaryEntry GetOrCreateEntry(List<TagDictionaryEntry> entries, string canonicalName)
    {
        if (TryFindEntry(entries, canonicalName, out TagDictionaryEntry? existingEntry))
        {
            return existingEntry;
        }

        TagDictionaryEntry entry = new()
        {
            CanonicalName = canonicalName,
            Aliases = new List<string>(),
            GlobalFrequency = 0,
        };

        entries.Add(entry);
        return entry;
    }

    private static bool TryFindEntry(IEnumerable<TagDictionaryEntry> entries, string canonicalName, [NotNullWhen(true)] out TagDictionaryEntry? entry)
    {
        entry = entries.FirstOrDefault(candidate => string.Equals(candidate.CanonicalName, canonicalName, StringComparison.OrdinalIgnoreCase));
        return entry is not null;
    }

    private static void AddAliasIfMissing(TagDictionaryEntry entry, string alias)
    {
        if (!entry.Aliases.Contains(alias, StringComparer.OrdinalIgnoreCase))
        {
            entry.Aliases.Add(alias);
        }
    }

    private static void RemoveSelfAliases(TagDictionaryEntry entry)
    {
        entry.Aliases = entry.Aliases
            .Where(alias => !string.Equals(alias, entry.CanonicalName, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(alias => alias, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void ValidateNoCircularAliases(IReadOnlyList<TagDictionaryEntry> entries)
    {
        foreach (TagDictionaryEntry entry in entries)
        {
            foreach (string alias in entry.Aliases)
            {
                string resolvedName = ResolveAliasUsingEntries(entries, alias);

                if (!string.Equals(resolvedName, entry.CanonicalName, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Circular alias detected.");
                }
            }
        }
    }

    private static TagDictionaryEntry CloneTagDictionaryEntry(TagDictionaryEntry entry)
    {
        return new TagDictionaryEntry
        {
            CanonicalName = entry.CanonicalName,
            Aliases = entry.Aliases
                .Select(alias => alias)
                .ToList(),
            GlobalFrequency = entry.GlobalFrequency,
        };
    }

    private async Task PersistTagStatisticsSnapshotAsync(Project project, IReadOnlyList<TagDictionaryEntry> entries, string tagFilesFingerprint)
    {
        project.TagDictionaryEntries = entries
            .Select(CloneTagDictionaryEntry)
            .ToList();
        project.State ??= new ProjectState();
        project.State.TagStatisticsCacheFingerprint = tagFilesFingerprint;
        await projectService.SaveProjectAsync(project);
    }

    private void InvalidateCachedProject(string projectId)
    {
        lock (cacheSync)
        {
            dictionaryEntriesCacheByProjectId.Remove(projectId);
        }
    }

    private void InvalidateCachedProjectForImage(string imagePath)
    {
        string? projectId = TryResolveCachedProjectIdForImagePath(imagePath);

        if (string.IsNullOrWhiteSpace(projectId))
        {
            return;
        }

        InvalidateCachedProject(projectId);
        messenger.Send(new TagDictionaryChangedMessage(projectId));
    }

    private string? TryResolveCachedProjectIdForImagePath(string imagePath)
    {
        string normalizedImagePath = Path.GetFullPath(imagePath);

        lock (cacheSync)
        {
            return projectRootPathByProjectId
                .OrderByDescending(entry => entry.Value.Length)
                .FirstOrDefault(entry => IsPathWithinRoot(normalizedImagePath, entry.Value))
                .Key;
        }
    }

    private static bool IsPathWithinRoot(string filePath, string rootPath)
    {
        string normalizedRootPath = Path.GetFullPath(rootPath);
        normalizedRootPath = Path.TrimEndingDirectorySeparator(normalizedRootPath) + Path.DirectorySeparatorChar;
        return filePath.StartsWith(normalizedRootPath, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildTagFilesFingerprint(IReadOnlyList<string> tagFilePaths)
    {
        StringBuilder fingerprintBuilder = new StringBuilder();

        foreach (string tagFilePath in tagFilePaths)
        {
            FileInfo fileInfo = new FileInfo(tagFilePath);
            fingerprintBuilder
                .Append(tagFilePath)
                .Append('|')
                .Append(fileInfo.Exists ? fileInfo.LastWriteTimeUtc.Ticks : 0L)
                .Append('|')
                .Append(fileInfo.Exists ? fileInfo.Length : 0L)
                .AppendLine();
        }

        byte[] fingerprintBytes = Encoding.UTF8.GetBytes(fingerprintBuilder.ToString());
        return Convert.ToHexString(SHA256.HashData(fingerprintBytes));
    }

    private static string SanitizeTagName(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Tag value is required.", parameterName);
        }

        return value.Trim();
    }
}
