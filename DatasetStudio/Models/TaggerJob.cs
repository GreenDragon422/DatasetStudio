using System.Threading.Tasks;

namespace DatasetStudio.Models;

public sealed class TaggerJob
{
    public TaggerJob(TaggerModelConfig modelConfig, string imagePath)
    {
        ModelConfig = modelConfig;
        ImagePath = imagePath;
        CompletionSource = new TaskCompletionSource<ImageTaggingResult>(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public TaggerModelConfig ModelConfig { get; }

    public string ImagePath { get; }

    public TaskCompletionSource<ImageTaggingResult> CompletionSource { get; }
}
