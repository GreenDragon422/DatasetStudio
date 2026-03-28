using DatasetStudio.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DatasetStudio.Services;

public sealed class ImagePreprocessor
{
    public async Task<(float[] tensorData, long[] shape)> PrepareBatchAsync(
        IReadOnlyList<string> imagePaths,
        LoadedTaggerModelState modelState,
        CancellationToken cancellationToken = default)
    {
        if (imagePaths.Count == 0)
        {
            throw new ArgumentException("At least one image is required for preprocessing.", nameof(imagePaths));
        }

        if (modelState.InputChannels != 3)
        {
            throw new InvalidOperationException("DatasetStudio currently supports 3-channel ONNX image taggers only.");
        }

        int pixelsPerImage = modelState.InputHeight * modelState.InputWidth * modelState.InputChannels;
        float[] tensorData = new float[imagePaths.Count * pixelsPerImage];

        for (int imageIndex = 0; imageIndex < imagePaths.Count; imageIndex += 1)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string imagePath = imagePaths[imageIndex];
            int imageOffset = imageIndex * pixelsPerImage;
            await WriteImageDataAsync(tensorData, imageOffset, imagePath, modelState, cancellationToken).ConfigureAwait(false);
        }

        long[] shape = modelState.InputLayout == TaggerInputLayout.Nhwc
            ? new long[] { imagePaths.Count, modelState.InputHeight, modelState.InputWidth, modelState.InputChannels }
            : new long[] { imagePaths.Count, modelState.InputChannels, modelState.InputHeight, modelState.InputWidth };
        return (tensorData, shape);
    }

    private static async Task WriteImageDataAsync(
        float[] tensorData,
        int imageOffset,
        string imagePath,
        LoadedTaggerModelState modelState,
        CancellationToken cancellationToken)
    {
        await using FileStream fileStream = File.OpenRead(imagePath);
        using Image<Rgba32> image = await Image.LoadAsync<Rgba32>(fileStream, cancellationToken).ConfigureAwait(false);
        using Image<Rgb24> preparedImage = PrepareImage(image, modelState.InputWidth, modelState.InputHeight);

        if (modelState.InputLayout == TaggerInputLayout.Nhwc)
        {
            WriteNhwcTensor(tensorData, imageOffset, preparedImage);
            return;
        }

        if (modelState.InputLayout == TaggerInputLayout.Nchw)
        {
            WriteNchwTensor(tensorData, imageOffset, preparedImage);
            return;
        }

        throw new InvalidOperationException("Unknown tagger input layout.");
    }

    private static Image<Rgb24> PrepareImage(Image<Rgba32> sourceImage, int targetWidth, int targetHeight)
    {
        using Image<Rgba32> alphaCompositedImage = new Image<Rgba32>(
            sourceImage.Width,
            sourceImage.Height,
            new Rgba32(255, 255, 255, 255));
        alphaCompositedImage.Mutate(context => context.DrawImage(sourceImage, 1.0f));

        Image<Rgb24> rgbImage = alphaCompositedImage.CloneAs<Rgb24>();
        int maxDimension = Math.Max(rgbImage.Width, rgbImage.Height);
        int padLeft = (maxDimension - rgbImage.Width) / 2;
        int padTop = (maxDimension - rgbImage.Height) / 2;

        Image<Rgb24> squareImage = new Image<Rgb24>(maxDimension, maxDimension, new Rgb24(255, 255, 255));
        squareImage.Mutate(context => context.DrawImage(rgbImage, new Point(padLeft, padTop), 1.0f));
        rgbImage.Dispose();

        if (squareImage.Width != targetWidth || squareImage.Height != targetHeight)
        {
            squareImage.Mutate(context => context.Resize(targetWidth, targetHeight, KnownResamplers.Bicubic));
        }

        return squareImage;
    }

    private static void WriteNhwcTensor(float[] tensorData, int imageOffset, Image<Rgb24> image)
    {
        int pixelOffset = imageOffset;
        image.ProcessPixelRows(pixelAccessor =>
        {
            for (int rowIndex = 0; rowIndex < pixelAccessor.Height; rowIndex += 1)
            {
                Span<Rgb24> row = pixelAccessor.GetRowSpan(rowIndex);
                for (int columnIndex = 0; columnIndex < row.Length; columnIndex += 1)
                {
                    Rgb24 pixel = row[columnIndex];
                    tensorData[pixelOffset] = pixel.B;
                    tensorData[pixelOffset + 1] = pixel.G;
                    tensorData[pixelOffset + 2] = pixel.R;
                    pixelOffset += 3;
                }
            }
        });
    }

    private static void WriteNchwTensor(float[] tensorData, int imageOffset, Image<Rgb24> image)
    {
        int planeSize = image.Width * image.Height;
        int blueOffset = imageOffset;
        int greenOffset = imageOffset + planeSize;
        int redOffset = imageOffset + (planeSize * 2);
        int pixelIndex = 0;

        image.ProcessPixelRows(pixelAccessor =>
        {
            for (int rowIndex = 0; rowIndex < pixelAccessor.Height; rowIndex += 1)
            {
                Span<Rgb24> row = pixelAccessor.GetRowSpan(rowIndex);
                for (int columnIndex = 0; columnIndex < row.Length; columnIndex += 1)
                {
                    Rgb24 pixel = row[columnIndex];
                    tensorData[blueOffset + pixelIndex] = pixel.B;
                    tensorData[greenOffset + pixelIndex] = pixel.G;
                    tensorData[redOffset + pixelIndex] = pixel.R;
                    pixelIndex += 1;
                }
            }
        });
    }
}
