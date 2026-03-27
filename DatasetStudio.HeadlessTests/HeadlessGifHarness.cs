using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;

namespace DatasetStudio_HeadlessTests;

public sealed class HeadlessGifCaptureOptions
{
    public string RunName { get; init; } = "headless_run";
    public string FramePrefix { get; init; } = "frame";
    public string GifFileName { get; init; } = "animation.gif";
    public int InitialDelayMs { get; init; } = 250;
    public int CaptureIntervalMs { get; init; } = 200;
    public int MaxFrames { get; init; } = 60;
    public int GifFrameDelayMs { get; init; } = 200;
    public ushort RepeatCount { get; init; } = 0;
    public bool CaptureInitialFrame { get; init; } = true;
}

public sealed class HeadlessGifCaptureResult
{
    public required string OutputFolder { get; init; }
    public required string GifPath { get; init; }
    public required IReadOnlyList<string> FramePaths { get; init; }

    public int FrameCount => FramePaths.Count;
}

public static class HeadlessGifHarness
{
    public static async Task<HeadlessGifCaptureResult> CaptureAsync(
        Window window,
        HeadlessGifCaptureOptions options,
        Func<Task>? startAction = null,
        Func<int, Task>? beforeEachFrameCapture = null,
        Func<int, TimeSpan, bool>? shouldStop = null,
        Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(options);
        ValidateOptions(options);

        Action<string> logger = log ?? (_ => { });
        string outputFolder = TestOutputHelper.CreateRunOutputFolder(options.RunName, clearExisting: true);
        List<string> framePaths = new List<string>(options.MaxFrames);

        if (options.InitialDelayMs > 0)
        {
            await Task.Delay(options.InitialDelayMs);
        }

        if (options.CaptureInitialFrame)
        {
            framePaths.Add(CaptureFrame(window, outputFolder, options.FramePrefix, framePaths.Count, logger));
        }

        if (startAction != null)
        {
            await startAction();
        }

        Stopwatch stopwatch = Stopwatch.StartNew();

        while (framePaths.Count < options.MaxFrames)
        {
            if (options.CaptureIntervalMs > 0)
            {
                await Task.Delay(options.CaptureIntervalMs);
            }

            if (beforeEachFrameCapture != null)
            {
                await beforeEachFrameCapture(framePaths.Count);
            }

            framePaths.Add(CaptureFrame(window, outputFolder, options.FramePrefix, framePaths.Count, logger));

            if (shouldStop != null && shouldStop(framePaths.Count, stopwatch.Elapsed))
            {
                logger($"Stopping capture after {framePaths.Count} frames.");
                break;
            }
        }

        if (framePaths.Count == 0)
        {
            throw new InvalidOperationException("No frames were captured.");
        }

        string gifPath = TestOutputHelper.GetOutputPath(outputFolder, options.GifFileName);
        TestOutputHelper.CreateAnimatedGif(framePaths, gifPath, options.GifFrameDelayMs, options.RepeatCount);
        logger($"Animated GIF created: {gifPath}");

        return new HeadlessGifCaptureResult
        {
            OutputFolder = outputFolder,
            GifPath = gifPath,
            FramePaths = framePaths
        };
    }

    private static string CaptureFrame(
        Window window,
        string outputFolder,
        string framePrefix,
        int frameNumber,
        Action<string> logger)
    {
        WriteableBitmap? bitmap = window.CaptureRenderedFrame();
        if (bitmap == null)
        {
            throw new InvalidOperationException("Captured bitmap was null.");
        }

        string framePath = TestOutputHelper.GetSequentialPath(outputFolder, framePrefix, frameNumber);
        bitmap.Save(framePath);
        logger($"Captured frame {frameNumber:D3}: {Path.GetFileName(framePath)}");
        return framePath;
    }

    private static void ValidateOptions(HeadlessGifCaptureOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.RunName))
        {
            throw new ArgumentException("RunName cannot be null or whitespace.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.FramePrefix))
        {
            throw new ArgumentException("FramePrefix cannot be null or whitespace.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.GifFileName))
        {
            throw new ArgumentException("GifFileName cannot be null or whitespace.", nameof(options));
        }

        if (options.MaxFrames <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "MaxFrames must be greater than zero.");
        }

        if (options.InitialDelayMs < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "InitialDelayMs must be non-negative.");
        }

        if (options.CaptureIntervalMs < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "CaptureIntervalMs must be non-negative.");
        }

        if (options.GifFrameDelayMs <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "GifFrameDelayMs must be greater than zero.");
        }
    }
}
