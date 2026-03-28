using Microsoft.ML.OnnxRuntime;
using System;
using System.Collections.Generic;

namespace DatasetStudio.Models;

public sealed class LoadedTaggerModelState : IDisposable
{
    public TaggerModelConfig ModelConfig { get; set; } = new TaggerModelConfig();

    public InferenceSession Session { get; set; } = null!;

    public string InputName { get; set; } = string.Empty;

    public string OutputName { get; set; } = string.Empty;

    public TaggerInputLayout InputLayout { get; set; }

    public int InputHeight { get; set; }

    public int InputWidth { get; set; }

    public int InputChannels { get; set; }

    public int OutputTagCount { get; set; }

    public IReadOnlyList<TaggerLabelDefinition> LabelDefinitions { get; set; } = Array.Empty<TaggerLabelDefinition>();

    public void Dispose()
    {
        Session?.Dispose();
    }
}
