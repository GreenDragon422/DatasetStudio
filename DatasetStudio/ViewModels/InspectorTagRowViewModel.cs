using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading.Tasks;

namespace DatasetStudio.ViewModels;

public sealed class InspectorTagRowViewModel
{
    private readonly Func<string, Task> executeTagActionAsync;

    public InspectorTagRowViewModel(string tag, Func<string, Task> executeTagActionAsync)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            throw new ArgumentException("Tag rows require a non-empty tag.", nameof(tag));
        }

        this.executeTagActionAsync = executeTagActionAsync ?? throw new ArgumentNullException(nameof(executeTagActionAsync));
        Tag = tag;
        Command = new AsyncRelayCommand(ExecuteAsync);
    }

    public string Tag { get; }

    public IAsyncRelayCommand Command { get; }

    private Task ExecuteAsync()
    {
        return executeTagActionAsync(Tag);
    }
}
