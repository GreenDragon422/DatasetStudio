using DatasetStudio.Models;
using DatasetStudio.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.IO;
using System.Threading.Tasks;

namespace DatasetStudio.Tests;

[TestFixture]
public class ImagePreprocessorTests
{
    [Test]
    public void PrepareBatchAsync_EmptyList_Throws()
    {
        ImagePreprocessor preprocessor = new ImagePreprocessor();
        LoadedTaggerModelState modelState = new LoadedTaggerModelState
        {
            InputLayout = TaggerInputLayout.Nhwc,
            InputHeight = 1,
            InputWidth = 1,
            InputChannels = 3,
        };

        ArgumentException exception = Assert.ThrowsAsync<ArgumentException>(async () =>
            await preprocessor.PrepareBatchAsync(Array.Empty<string>(), modelState).ConfigureAwait(false));

        Assert.That(exception.Message, Does.Contain("At least one image"));
    }

    [Test]
    public async Task PrepareBatchAsync_NhwcModel_WritesBgrPixelsInNhwcOrder()
    {
        string imagePath = await CreateSinglePixelImageAsync(new Rgba32(10, 20, 30, 255)).ConfigureAwait(false);
        ImagePreprocessor preprocessor = new ImagePreprocessor();
        LoadedTaggerModelState modelState = new LoadedTaggerModelState
        {
            InputLayout = TaggerInputLayout.Nhwc,
            InputHeight = 1,
            InputWidth = 1,
            InputChannels = 3,
        };

        (float[] tensorData, long[] shape) = await preprocessor
            .PrepareBatchAsync(new[] { imagePath }, modelState)
            .ConfigureAwait(false);

        Assert.That(shape, Is.EqualTo(new long[] { 1, 1, 1, 3 }));
        Assert.That(tensorData, Is.EqualTo(new float[] { 30f, 20f, 10f }));
    }

    [Test]
    public async Task PrepareBatchAsync_NchwModel_WritesBgrPixelsInPlaneOrder()
    {
        string imagePath = await CreateSinglePixelImageAsync(new Rgba32(10, 20, 30, 255)).ConfigureAwait(false);
        ImagePreprocessor preprocessor = new ImagePreprocessor();
        LoadedTaggerModelState modelState = new LoadedTaggerModelState
        {
            InputLayout = TaggerInputLayout.Nchw,
            InputHeight = 1,
            InputWidth = 1,
            InputChannels = 3,
        };

        (float[] tensorData, long[] shape) = await preprocessor
            .PrepareBatchAsync(new[] { imagePath }, modelState)
            .ConfigureAwait(false);

        Assert.That(shape, Is.EqualTo(new long[] { 1, 3, 1, 1 }));
        Assert.That(tensorData, Is.EqualTo(new float[] { 30f, 20f, 10f }));
    }

    [Test]
    public async Task PrepareBatchAsync_UnsupportedChannelCount_Throws()
    {
        string imagePath = await CreateSinglePixelImageAsync(new Rgba32(10, 20, 30, 255)).ConfigureAwait(false);
        ImagePreprocessor preprocessor = new ImagePreprocessor();
        LoadedTaggerModelState modelState = new LoadedTaggerModelState
        {
            InputLayout = TaggerInputLayout.Nhwc,
            InputHeight = 1,
            InputWidth = 1,
            InputChannels = 4,
        };

        InvalidOperationException exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await preprocessor.PrepareBatchAsync(new[] { imagePath }, modelState).ConfigureAwait(false));

        Assert.That(exception.Message, Does.Contain("3-channel"));
    }

    private static async Task<string> CreateSinglePixelImageAsync(Rgba32 color)
    {
        string filePath = Path.Combine(
            Path.GetTempPath(),
            "DatasetStudioTests",
            Guid.NewGuid().ToString("N"),
            "pixel.png");
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        using Image<Rgba32> image = new Image<Rgba32>(1, 1, color);
        await image.SaveAsPngAsync(filePath).ConfigureAwait(false);
        return filePath;
    }
}
