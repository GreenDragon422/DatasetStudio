using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DatasetStudio.Messages;
using DatasetStudio.Models;
using DatasetStudio.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace DatasetStudio.ViewModels;

public partial class TagDictionaryViewModel : ScreenViewModelBase, INavigationAware
{
    private const int FrequentTagThreshold = 5;
    private const string AllTagsCategory = "All Tags";
    private const string NeedsAliasCategory = "Needs Alias";
    private const string OrphanedTagsCategory = "Orphaned Tags";
    private const string FrequentTagsCategory = "Frequent Tags";

    private readonly IMessenger messenger;
    private readonly ITagDictionaryService tagDictionaryService;
    private TagDictionaryRowViewModel? pendingMergeSourceEntry;

    public TagDictionaryViewModel(ITagDictionaryService tagDictionaryService, IMessenger messenger)
        : base(messenger)
    {
        this.tagDictionaryService = tagDictionaryService ?? throw new ArgumentNullException(nameof(tagDictionaryService));
        this.messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));

        Categories = new ObservableCollection<string>
        {
            AllTagsCategory,
            NeedsAliasCategory,
            OrphanedTagsCategory,
            FrequentTagsCategory,
        };
        AllEntries = new ObservableCollection<TagDictionaryRowViewModel>();
        FilteredEntries = new ObservableCollection<TagDictionaryRowViewModel>();
        SelectedCategory = AllTagsCategory;
        StatusText = "Open a project to manage its tag dictionary.";

        messenger.Register<TagDictionaryChangedMessage>(this, static (recipient, message) =>
        {
            TagDictionaryViewModel viewModel = (TagDictionaryViewModel)recipient;

            if (string.Equals(viewModel.CurrentProjectId, message.ProjectId, StringComparison.OrdinalIgnoreCase))
            {
                _ = viewModel.LoadEntriesAsync();
            }
        });
    }

    public ObservableCollection<string> Categories { get; }

    public string CurrentProjectId { get; private set; } = string.Empty;

    [ObservableProperty]
    private ObservableCollection<TagDictionaryRowViewModel> allEntries;

    [ObservableProperty]
    private ObservableCollection<TagDictionaryRowViewModel> filteredEntries;

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private string selectedCategory;

    [ObservableProperty]
    private TagDictionaryRowViewModel? selectedEntry;

    [ObservableProperty]
    private bool isEditing;

    public void OnNavigatedTo(object parameter)
    {
        if (parameter is string projectId)
        {
            CurrentProjectId = projectId;
            StatusText = "Loading tag dictionary...";
            _ = LoadEntriesAsync();
        }
    }

    [RelayCommand]
    private async Task LoadEntriesAsync()
    {
        if (string.IsNullOrWhiteSpace(CurrentProjectId))
        {
            StatusText = "No active project selected for the tag dictionary.";
            AllEntries.Clear();
            FilteredEntries.Clear();
            return;
        }

        IReadOnlyList<TagDictionaryEntry> entries = await tagDictionaryService.GetAllEntriesAsync(CurrentProjectId).ConfigureAwait(false);
        List<TagDictionaryRowViewModel> rowViewModels = entries
            .OrderBy(entry => entry.CanonicalName, StringComparer.OrdinalIgnoreCase)
            .Select(entry => new TagDictionaryRowViewModel(entry))
            .ToList();

        AllEntries = new ObservableCollection<TagDictionaryRowViewModel>(rowViewModels);
        ApplyFilters();
        StatusText = string.Format("Loaded {0} tag entr{1}.", AllEntries.Count, AllEntries.Count == 1 ? "y" : "ies");
    }

    [RelayCommand]
    private void NewTag()
    {
        SearchText = string.Empty;
        SelectedCategory = AllTagsCategory;
        CancelAllEditing();

        TagDictionaryRowViewModel newEntry = new(new TagDictionaryEntry())
        {
            IsEditing = true,
            IsNewEntry = true,
        };

        AllEntries.Insert(0, newEntry);
        ApplyFilters();
        SelectedEntry = newEntry;
        IsEditing = true;
        StatusText = "Creating a new tag entry.";
    }

    public void BeginEditing(TagDictionaryRowViewModel? entry)
    {
        if (entry is null)
        {
            return;
        }

        CancelAllEditing();
        entry.IsEditing = true;
        SelectedEntry = entry;
        IsEditing = true;
        StatusText = string.Format("Editing {0}.", string.IsNullOrWhiteSpace(entry.CanonicalName) ? "new tag" : entry.CanonicalName);
    }

    public void CancelEditing(TagDictionaryRowViewModel? entry)
    {
        if (entry is null)
        {
            return;
        }

        if (entry.IsNewEntry)
        {
            AllEntries.Remove(entry);
            ApplyFilters();
        }
        else
        {
            entry.CanonicalName = entry.OriginalCanonicalName;
        }

        entry.IsEditing = false;
        entry.IsNewEntry = false;
        IsEditing = AllEntries.Any(candidate => candidate.IsEditing);
        StatusText = "Edit cancelled.";
    }

    public async Task SaveEntryAsync(TagDictionaryRowViewModel? entry)
    {
        if (entry is null)
        {
            return;
        }

        string sanitizedCanonicalName = entry.CanonicalName.Trim();

        if (string.IsNullOrWhiteSpace(sanitizedCanonicalName))
        {
            StatusText = "Tag name cannot be empty.";
            return;
        }

        if (entry.IsNewEntry)
        {
            await tagDictionaryService.RenameTagAsync(CurrentProjectId, sanitizedCanonicalName, sanitizedCanonicalName).ConfigureAwait(false);
        }
        else if (!string.Equals(entry.OriginalCanonicalName, sanitizedCanonicalName, StringComparison.OrdinalIgnoreCase))
        {
            await tagDictionaryService.RenameTagAsync(CurrentProjectId, entry.OriginalCanonicalName, sanitizedCanonicalName).ConfigureAwait(false);
        }

        await tagDictionaryService.SetAliasesAsync(CurrentProjectId, sanitizedCanonicalName, entry.Aliases).ConfigureAwait(false);
        entry.IsEditing = false;
        entry.IsNewEntry = false;
        pendingMergeSourceEntry = null;
        messenger.Send(new TagDictionaryChangedMessage(CurrentProjectId));
        await LoadEntriesAsync().ConfigureAwait(false);
        StatusText = string.Format("Saved tag entry {0}.", sanitizedCanonicalName);
    }

    public async Task BeginMergeAsync(TagDictionaryRowViewModel? entry)
    {
        if (entry is null)
        {
            return;
        }

        if (pendingMergeSourceEntry is null)
        {
            pendingMergeSourceEntry = entry;
            StatusText = string.Format("Select a target row and click Merge again to merge {0}.", entry.CanonicalName);
            return;
        }

        if (ReferenceEquals(pendingMergeSourceEntry, entry))
        {
            pendingMergeSourceEntry = null;
            StatusText = "Merge cancelled.";
            return;
        }

        await tagDictionaryService.MergeTagsAsync(CurrentProjectId, pendingMergeSourceEntry.CanonicalName, entry.CanonicalName).ConfigureAwait(false);
        pendingMergeSourceEntry = null;
        messenger.Send(new TagDictionaryChangedMessage(CurrentProjectId));
        await LoadEntriesAsync().ConfigureAwait(false);
        StatusText = "Tags merged successfully.";
    }

    public async Task DeleteEntryAsync(TagDictionaryRowViewModel? entry, bool removeFromFiles)
    {
        if (entry is null)
        {
            return;
        }

        await tagDictionaryService.DeleteTagAsync(CurrentProjectId, entry.CanonicalName, removeFromFiles).ConfigureAwait(false);
        pendingMergeSourceEntry = null;
        messenger.Send(new TagDictionaryChangedMessage(CurrentProjectId));
        await LoadEntriesAsync().ConfigureAwait(false);
        StatusText = removeFromFiles
            ? string.Format("Deleted {0} and removed it from tag files.", entry.CanonicalName)
            : string.Format("Deleted {0} from the dictionary.", entry.CanonicalName);
    }

    partial void OnSearchTextChanged(string value)
    {
        _ = value;
        ApplyFilters();
    }

    partial void OnSelectedCategoryChanged(string value)
    {
        _ = value;
        ApplyFilters();
    }

    private void CancelAllEditing()
    {
        foreach (TagDictionaryRowViewModel entry in AllEntries)
        {
            entry.IsEditing = false;
            entry.IsNewEntry = false;
        }

        IsEditing = false;
    }

    private void ApplyFilters()
    {
        IEnumerable<TagDictionaryRowViewModel> filtered = AllEntries;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            filtered = filtered.Where(entry =>
                entry.CanonicalName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                || entry.DisplayAliases.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        filtered = SelectedCategory switch
        {
            NeedsAliasCategory => filtered.Where(entry => entry.Aliases.Count == 0),
            OrphanedTagsCategory => filtered.Where(entry => entry.GlobalFrequency == 0),
            FrequentTagsCategory => filtered.Where(entry => entry.GlobalFrequency > FrequentTagThreshold),
            _ => filtered,
        };

        FilteredEntries = new ObservableCollection<TagDictionaryRowViewModel>(filtered);
    }
}
