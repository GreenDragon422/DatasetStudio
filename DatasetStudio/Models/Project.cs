using System;
using System.Collections.Generic;

namespace DatasetStudio.Models;

public sealed class Project
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string RootFolderPath { get; set; } = string.Empty;

    public List<WorkflowStage> Stages { get; set; } = new List<WorkflowStage>();

    public List<string> PrefixTags { get; set; } = new List<string>();

    public string AiModelName { get; set; } = string.Empty;

    public DateTime LastModified { get; set; }

    public List<TagDictionaryEntry> TagDictionaryEntries { get; set; } = new List<TagDictionaryEntry>();

    public ProjectState State { get; set; } = new ProjectState();
}
