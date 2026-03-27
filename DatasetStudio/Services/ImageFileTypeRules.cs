using System;
using System.Collections.Generic;
using System.IO;

namespace DatasetStudio.Services;

public static class ImageFileTypeRules
{
    private static readonly HashSet<string> SupportedImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".webp",
        ".bmp",
    };

    public static bool IsSupportedImagePath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        return SupportedImageExtensions.Contains(Path.GetExtension(filePath));
    }
}
