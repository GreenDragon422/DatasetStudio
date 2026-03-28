using DatasetStudio.Models;
using Microsoft.ML.OnnxRuntime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace DatasetStudio.Services;

public sealed class TaggerSession : ITaggerSession, IDisposable
{
    private readonly Channel<TaggerJob> jobChannel;
    private readonly CancellationTokenSource shutdownCancellationTokenSource;
    private readonly Task workerTask;
    private readonly ImagePreprocessor imagePreprocessor;
    private readonly TagPostProcessor tagPostProcessor;
    private readonly Queue<TaggerJob> deferredJobs;
    private LoadedTaggerModelState? loadedModelState;
    private bool isDisposed;

    public TaggerSession(ImagePreprocessor imagePreprocessor, TagPostProcessor tagPostProcessor)
    {
        this.imagePreprocessor = imagePreprocessor ?? throw new ArgumentNullException(nameof(imagePreprocessor));
        this.tagPostProcessor = tagPostProcessor ?? throw new ArgumentNullException(nameof(tagPostProcessor));
        jobChannel = Channel.CreateUnbounded<TaggerJob>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });
        deferredJobs = new Queue<TaggerJob>();
        shutdownCancellationTokenSource = new CancellationTokenSource();
        workerTask = Task.Run(() => ProcessJobsAsync(shutdownCancellationTokenSource.Token));
    }

    public async Task<ImageTaggingResult> TagImageAsync(
        TaggerModelConfig modelConfig,
        string imageFilePath,
        CancellationToken cancellationToken = default)
    {
        if (modelConfig is null)
        {
            throw new ArgumentNullException(nameof(modelConfig));
        }

        if (string.IsNullOrWhiteSpace(imageFilePath))
        {
            throw new ArgumentException("An image file path is required.", nameof(imageFilePath));
        }

        ThrowIfDisposed();

        TaggerJob job = new TaggerJob(modelConfig, imageFilePath);
        if (cancellationToken.CanBeCanceled)
        {
            cancellationToken.Register(() => job.CompletionSource.TrySetCanceled(cancellationToken));
        }

        await jobChannel.Writer.WriteAsync(job, cancellationToken).ConfigureAwait(false);
        return await job.CompletionSource.Task.ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        shutdownCancellationTokenSource.Cancel();
        jobChannel.Writer.TryComplete();

        try
        {
            workerTask.GetAwaiter().GetResult();
        }
        catch
        {
        }

        loadedModelState?.Dispose();
        shutdownCancellationTokenSource.Dispose();
    }

    private async Task ProcessJobsAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TaggerJob nextJob = await DequeueNextJobAsync(cancellationToken).ConfigureAwait(false);
                List<TaggerJob>? batch = null;

                try
                {
                    LoadedTaggerModelState modelState = await EnsureLoadedModelStateAsync(nextJob.ModelConfig, cancellationToken).ConfigureAwait(false);
                    batch = CollectBatch(nextJob, modelState.ModelConfig.CacheKey);
                    await ExecuteBatchAsync(batch, modelState, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    ResetLoadedModelState();
                    IReadOnlyList<TaggerJob> failedJobs = batch is not null
                        ? batch
                        : new[] { nextJob };
                    FailJobs(failedJobs, exception);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            FailOutstandingJobs();
        }
    }

    private async Task<TaggerJob> DequeueNextJobAsync(CancellationToken cancellationToken)
    {
        if (deferredJobs.Count > 0)
        {
            return deferredJobs.Dequeue();
        }

        return await jobChannel.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
    }

    private List<TaggerJob> CollectBatch(TaggerJob firstJob, string cacheKey)
    {
        List<TaggerJob> batch = new List<TaggerJob> { firstJob };
        while (batch.Count < firstJob.ModelConfig.BatchSize)
        {
            TaggerJob? nextJob = null;
            if (deferredJobs.Count > 0)
            {
                nextJob = deferredJobs.Dequeue();
            }
            else if (jobChannel.Reader.TryRead(out TaggerJob? queuedJob) && queuedJob is not null)
            {
                nextJob = queuedJob;
            }

            if (nextJob is null)
            {
                break;
            }

            if (string.Equals(nextJob.ModelConfig.CacheKey, cacheKey, StringComparison.OrdinalIgnoreCase))
            {
                batch.Add(nextJob);
                continue;
            }

            deferredJobs.Enqueue(nextJob);
            break;
        }

        return batch;
    }

    private async Task ExecuteBatchAsync(
        IReadOnlyList<TaggerJob> batch,
        LoadedTaggerModelState modelState,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> imagePaths = batch.Select(job => job.ImagePath).ToList();
        (float[] tensorData, long[] shape) = await imagePreprocessor.PrepareBatchAsync(imagePaths, modelState, cancellationToken).ConfigureAwait(false);

        using RunOptions runOptions = new RunOptions();
        using OrtValue inputValue = OrtValue.CreateTensorValueFromMemory<float>(tensorData, shape);
        using IDisposableReadOnlyCollection<OrtValue> outputs = modelState.Session.Run(
            runOptions,
            new[] { modelState.InputName },
            new[] { inputValue },
            new[] { modelState.OutputName });

        OrtValue outputValue = outputs.First();
        ReadOnlySpan<float> outputData = outputValue.GetTensorDataAsSpan<float>();
        int tagCount = modelState.OutputTagCount;
        int expectedOutputLength = batch.Count * tagCount;
        if (outputData.Length < expectedOutputLength)
        {
            throw new InvalidOperationException(
                "The selected ONNX model returned fewer tag scores than expected for the processed batch.");
        }

        for (int batchIndex = 0; batchIndex < batch.Count; batchIndex += 1)
        {
            ReadOnlySpan<float> imageScores = outputData.Slice(batchIndex * tagCount, tagCount);
            ImageTaggingResult result = tagPostProcessor.CreateResult(
                modelState.ModelConfig,
                modelState.LabelDefinitions,
                imageScores);
            batch[batchIndex].CompletionSource.TrySetResult(result);
        }
    }

    private async Task<LoadedTaggerModelState> EnsureLoadedModelStateAsync(
        TaggerModelConfig modelConfig,
        CancellationToken cancellationToken)
    {
        if (loadedModelState is not null
            && string.Equals(loadedModelState.ModelConfig.CacheKey, modelConfig.CacheKey, StringComparison.OrdinalIgnoreCase))
        {
            return loadedModelState;
        }

        LoadedTaggerModelState? previousState = loadedModelState;
        LoadedTaggerModelState newState = await LoadModelStateAsync(modelConfig, cancellationToken).ConfigureAwait(false);
        loadedModelState = newState;
        previousState?.Dispose();
        return newState;
    }

    private void ResetLoadedModelState()
    {
        LoadedTaggerModelState? previousState = loadedModelState;
        loadedModelState = null;
        previousState?.Dispose();
    }

    private async Task<LoadedTaggerModelState> LoadModelStateAsync(
        TaggerModelConfig modelConfig,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using SessionOptions sessionOptions = new SessionOptions
        {
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            EnableMemoryPattern = true,
            LogId = "DatasetStudioTagger",
        };

        try
        {
            sessionOptions.AppendExecutionProvider_CUDA(0);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "Could not initialize the ONNX Runtime CUDA execution provider. Ensure ONNX Runtime GPU is installed and CUDA is available.",
                exception);
        }

        InferenceSession? session = null;
        try
        {
            session = new InferenceSession(modelConfig.ModelFilePath, sessionOptions);
            KeyValuePair<string, NodeMetadata> inputMetadata = session.InputMetadata.Single();
            KeyValuePair<string, NodeMetadata> outputMetadata = session.OutputMetadata.Single();

            (TaggerInputLayout inputLayout, int inputHeight, int inputWidth, int inputChannels) = ResolveInputShape(inputMetadata.Value);
            int outputTagCount = ResolveOutputTagCount(outputMetadata.Value);
            IReadOnlyList<TaggerLabelDefinition> labels = tagPostProcessor.LoadLabels(modelConfig.TagCsvPath);
            if (labels.Count != outputTagCount)
            {
                throw new InvalidOperationException(
                    "The loaded selected_tags.csv does not match the ONNX output width for the selected model.");
            }

            return await Task.FromResult(new LoadedTaggerModelState
            {
                ModelConfig = modelConfig,
                Session = session,
                InputName = inputMetadata.Key,
                OutputName = outputMetadata.Key,
                InputLayout = inputLayout,
                InputHeight = inputHeight,
                InputWidth = inputWidth,
                InputChannels = inputChannels,
                OutputTagCount = outputTagCount,
                LabelDefinitions = labels,
            }).ConfigureAwait(false);
        }
        catch
        {
            session?.Dispose();
            throw;
        }
    }

    private static (TaggerInputLayout layout, int height, int width, int channels) ResolveInputShape(NodeMetadata inputMetadata)
    {
        if (inputMetadata.ElementType != typeof(float))
        {
            throw new InvalidOperationException("The selected ONNX model must accept float image tensors.");
        }

        int[] dimensions = inputMetadata.Dimensions;
        if (dimensions.Length != 4)
        {
            throw new InvalidOperationException("The selected ONNX model must expose a 4D image tensor input.");
        }

        if (dimensions[3] == 3)
        {
            return (TaggerInputLayout.Nhwc, dimensions[1], dimensions[2], dimensions[3]);
        }

        if (dimensions[1] == 3)
        {
            return (TaggerInputLayout.Nchw, dimensions[2], dimensions[3], dimensions[1]);
        }

        throw new InvalidOperationException("Could not determine whether the ONNX input layout is NHWC or NCHW.");
    }

    private static int ResolveOutputTagCount(NodeMetadata outputMetadata)
    {
        if (outputMetadata.ElementType != typeof(float))
        {
            throw new InvalidOperationException("The selected ONNX model must expose float tag scores.");
        }

        int[] dimensions = outputMetadata.Dimensions;
        if (dimensions.Length < 2)
        {
            throw new InvalidOperationException("The selected ONNX model must expose a batch x tag output tensor.");
        }

        int outputTagCount = dimensions[dimensions.Length - 1];
        if (outputTagCount <= 0)
        {
            throw new InvalidOperationException("The selected ONNX model reported an invalid output width.");
        }

        return outputTagCount;
    }

    private void FailOutstandingJobs()
    {
        while (deferredJobs.Count > 0)
        {
            TaggerJob deferredJob = deferredJobs.Dequeue();
            deferredJob.CompletionSource.TrySetCanceled();
        }

        while (jobChannel.Reader.TryRead(out TaggerJob? queuedJob))
        {
            if (queuedJob is not null)
            {
                queuedJob.CompletionSource.TrySetCanceled();
            }
        }
    }

    private static void FailJobs(IReadOnlyList<TaggerJob> jobs, Exception exception)
    {
        foreach (TaggerJob job in jobs)
        {
            job.CompletionSource.TrySetException(exception);
        }
    }

    private void ThrowIfDisposed()
    {
        if (isDisposed)
        {
            throw new ObjectDisposedException(nameof(TaggerSession));
        }
    }
}
