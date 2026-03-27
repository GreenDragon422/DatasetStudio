using CommunityToolkit.Mvvm.ComponentModel;
using DatasetStudio.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DatasetStudio.ViewModels;

public partial class TagDictionaryRowViewModel : ObservableObject
{
    public TagDictionaryRowViewModel(TagDictionaryEntry entry)
    {
        OriginalCanonicalName = entry.CanonicalName;
        CanonicalName = entry.CanonicalName;
        GlobalFrequency = entry.GlobalFrequency;
        EditableAliasesText = string.Join(", ", entry.Aliases.OrderBy(alias => alias, StringComparer.OrdinalIgnoreCase));
    }

    public string OriginalCanonicalName { get; }

    [ObservableProperty]
    private string canonicalName = string.Empty;

    [ObservableProperty]
    private int globalFrequency;

    [ObservableProperty]
    private bool isEditing;

    [ObservableProperty]
    private bool isNewEntry;

    [ObservableProperty]
    private string editableAliasesText = string.Empty;

    public IReadOnlyList<string> Aliases
    {
        get
        {
            return EditableAliasesText
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(alias => alias.Trim())
                .Where(alias => !string.IsNullOrWhiteSpace(alias))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(alias => alias, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    public string DisplayAliases
    {
        get
        {
            return string.Join(", ", Aliases);
        }
    }

    partial void OnEditableAliasesTextChanged(string value)
    {
        _ = value;
        OnPropertyChanged(nameof(Aliases));
        OnPropertyChanged(nameof(DisplayAliases));
    }
}
