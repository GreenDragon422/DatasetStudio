using System;
using Avalonia.Input;

namespace DatasetStudio.Views;

public sealed class ScreenShortcut
{
    private readonly Action executeAction;
    private readonly Func<bool>? isAvailable;
    private readonly Func<KeyEventArgs, bool> matchesShortcut;

    public ScreenShortcut(
        Func<KeyEventArgs, bool> matchesShortcut,
        string gestureText,
        string description,
        Action executeAction,
        bool allowWhenTextInputFocused = false,
        Func<bool>? isAvailable = null)
    {
        this.matchesShortcut = matchesShortcut ?? throw new ArgumentNullException(nameof(matchesShortcut));
        GestureText = string.IsNullOrWhiteSpace(gestureText)
            ? throw new ArgumentException("Shortcut gesture text is required.", nameof(gestureText))
            : gestureText;
        Description = string.IsNullOrWhiteSpace(description)
            ? throw new ArgumentException("Shortcut description is required.", nameof(description))
            : description;
        this.executeAction = executeAction ?? throw new ArgumentNullException(nameof(executeAction));
        this.isAvailable = isAvailable;
        AllowWhenTextInputFocused = allowWhenTextInputFocused;
    }

    public bool AllowWhenTextInputFocused { get; }

    public string Description { get; }

    public string GestureText { get; }

    public bool IsVisible(bool hasEditableTextInputFocus)
    {
        if (hasEditableTextInputFocus && !AllowWhenTextInputFocused)
        {
            return false;
        }

        if (isAvailable is not null && !isAvailable())
        {
            return false;
        }

        return true;
    }

    public bool Matches(KeyEventArgs eventArgs, bool hasEditableTextInputFocus)
    {
        if (!IsVisible(hasEditableTextInputFocus))
        {
            return false;
        }

        return matchesShortcut(eventArgs);
    }

    public string ToHintSegment()
    {
        return string.Format("{0} {1}", GestureText, Description);
    }

    public void Execute()
    {
        executeAction();
    }
}
