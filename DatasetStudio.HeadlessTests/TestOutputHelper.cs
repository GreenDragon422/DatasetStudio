using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.PixelFormats;

namespace DatasetStudio_HeadlessTests;

public static class TestOutputHelper
{
    private static readonly string TestOutputFolder = Path.Combine(
        AppContext.BaseDirectory,
        "..",
        "..",
        "..",
        "TestOutputs");

    public static void ClearTestOutputs()
    {
        if (!Directory.Exists(TestOutputFolder))
        {
            Directory.CreateDirectory(TestOutputFolder);
            return;
        }

        DirectoryInfo directoryInfo = new DirectoryInfo(TestOutputFolder);
        foreach (FileSystemInfo entry in directoryInfo.EnumerateFileSystemInfos())
        {
            if (entry is FileInfo fileInfo)
            {
                DeleteFileWithRetry(fileInfo.FullName);
                continue;
            }

            if (entry is DirectoryInfo subDirectory)
            {
                DeleteDirectoryWithRetry(subDirectory.FullName);
            }
        }
    }

    public static string CreateRunOutputFolder(string runName, bool clearExisting = true)
    {
        if (string.IsNullOrWhiteSpace(runName))
        {
            throw new ArgumentException("Run name cannot be null or whitespace.", nameof(runName));
        }

        string sanitizedRunName = SanitizePathSegment(runName.Trim());
        string outputFolder = Path.Combine(GetTestOutputFolder(), sanitizedRunName);

        if (clearExisting && Directory.Exists(outputFolder))
        {
            DeleteDirectoryWithRetry(outputFolder);
        }

        Directory.CreateDirectory(outputFolder);
        return outputFolder;
    }

    public static string GetSequentialPath(string outputFolder, string prefix, int sequenceNumber, string extension = "png")
    {
        if (string.IsNullOrWhiteSpace(outputFolder))
        {
            throw new ArgumentException("Output folder cannot be null or whitespace.", nameof(outputFolder));
        }

        if (string.IsNullOrWhiteSpace(prefix))
        {
            throw new ArgumentException("Prefix cannot be null or whitespace.", nameof(prefix));
        }

        string normalizedExtension = extension.Trim().TrimStart('.');
        if (string.IsNullOrWhiteSpace(normalizedExtension))
        {
            normalizedExtension = "png";
        }

        Directory.CreateDirectory(outputFolder);
        string fileName = $"{prefix}_{sequenceNumber:D3}.{normalizedExtension}";
        return Path.Combine(outputFolder, fileName);
    }

    public static string GetOutputPath(string outputFolder, string fileName)
    {
        if (string.IsNullOrWhiteSpace(outputFolder))
        {
            throw new ArgumentException("Output folder cannot be null or whitespace.", nameof(outputFolder));
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("Filename cannot be null or whitespace.", nameof(fileName));
        }

        Directory.CreateDirectory(outputFolder);
        return Path.Combine(outputFolder, fileName);
    }

    public static string GetTestOutputFolder()
    {
        Directory.CreateDirectory(TestOutputFolder);
        return TestOutputFolder;
    }

    private static void DeleteFileWithRetry(string filePath)
    {
        ExecuteWithRetry(() =>
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        });
    }

    private static void DeleteDirectoryWithRetry(string directoryPath)
    {
        ExecuteWithRetry(() =>
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, recursive: true);
            }
        });
    }

    private static void ExecuteWithRetry(Action action)
    {
        const int MaxAttempts = 8;

        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                action();
                return;
            }
            catch (IOException) when (attempt < MaxAttempts)
            {
                Thread.Sleep(50 * attempt);
            }
            catch (UnauthorizedAccessException) when (attempt < MaxAttempts)
            {
                Thread.Sleep(50 * attempt);
            }
        }
    }

    public static void CreateAnimatedGif(IEnumerable<string> frameFiles, string outputPath, int frameDelayMs = 200, ushort repeatCount = 0)
    {
        ArgumentNullException.ThrowIfNull(frameFiles);

        string[] resolvedFrameFiles = frameFiles.ToArray();
        if (resolvedFrameFiles.Length == 0)
        {
            throw new InvalidOperationException("At least one frame file is required to create a GIF.");
        }

        foreach (string frameFile in resolvedFrameFiles)
        {
            if (!File.Exists(frameFile))
            {
                throw new FileNotFoundException($"Frame file does not exist: {frameFile}", frameFile);
            }
        }

        string? outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        int frameDelay = Math.Max(1, frameDelayMs / 10);

        using Image<Rgba32> gif = Image.Load<Rgba32>(resolvedFrameFiles[0]);
        GifMetadata gifMetadata = gif.Metadata.GetGifMetadata();
        gifMetadata.RepeatCount = repeatCount;

        GifFrameMetadata frameMetadata = gif.Frames.RootFrame.Metadata.GetGifMetadata();
        frameMetadata.FrameDelay = frameDelay;

        for (int index = 1; index < resolvedFrameFiles.Length; index++)
        {
            using Image<Rgba32> frame = Image.Load<Rgba32>(resolvedFrameFiles[index]);
            frameMetadata = frame.Frames.RootFrame.Metadata.GetGifMetadata();
            frameMetadata.FrameDelay = frameDelay;
            gif.Frames.AddFrame(frame.Frames.RootFrame);
        }

        gif.SaveAsGif(outputPath);
    }

    private static string SanitizePathSegment(string segment)
    {
        char[] invalidChars = Path.GetInvalidFileNameChars();
        char[] sanitizedChars = segment
            .Select(character => invalidChars.Contains(character) ? '_' : character)
            .ToArray();

        string sanitized = new string(sanitizedChars).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "run" : sanitized;
    }
}
