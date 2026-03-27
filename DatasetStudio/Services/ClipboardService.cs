using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DatasetStudio.Services;

public sealed class ClipboardService : IClipboardService
{
    public async Task CopyTagsAsync(IReadOnlyList<string> tags)
    {
        Avalonia.Input.Platform.IClipboard? clipboard = GetClipboard();

        if (clipboard is null)
        {
            return;
        }

        List<string> sanitizedTags = tags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        string clipboardText = string.Join(", ", sanitizedTags);
        await clipboard.SetTextAsync(clipboardText);
    }

    public async Task<IReadOnlyList<string>> PasteTagsAsync()
    {
        Avalonia.Input.Platform.IClipboard? clipboard = GetClipboard();

        if (clipboard is null)
        {
            return [];
        }

        string? clipboardText = await ClipboardExtensions.TryGetTextAsync(clipboard);

        if (string.IsNullOrWhiteSpace(clipboardText))
        {
            return [];
        }

        return clipboardText
            .Split(',')
            .Select(tag => tag.Trim())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static Avalonia.Input.Platform.IClipboard? GetClipboard()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return null;
        }

        return desktop.MainWindow?.Clipboard;
    }
}
