using System;

namespace DatasetStudio.ViewModels;

public sealed class HintBarItemViewModel
{
    public HintBarItemViewModel(string keyText, string description)
    {
        KeyText = string.IsNullOrWhiteSpace(keyText)
            ? throw new ArgumentException("Hint-bar key text is required.", nameof(keyText))
            : keyText;
        Description = string.IsNullOrWhiteSpace(description)
            ? throw new ArgumentException("Hint-bar description is required.", nameof(description))
            : description;
    }

    public string Description { get; }

    public string KeyText { get; }
}
