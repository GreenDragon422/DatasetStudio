namespace DatasetStudio.ViewModels;

public sealed class BatchTagSuggestionViewModel
{
    public BatchTagSuggestionViewModel(string tag, int frequency)
    {
        Tag = tag;
        Frequency = frequency;
    }

    public string Tag { get; }

    public int Frequency { get; }

    public bool HasFrequency
    {
        get
        {
            return Frequency > 0;
        }
    }
}
