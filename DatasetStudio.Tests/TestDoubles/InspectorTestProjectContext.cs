using CommunityToolkit.Mvvm.Messaging;
using DatasetStudio.Models;
using DatasetStudio.Services;

namespace DatasetStudio.Tests.TestDoubles;

public sealed class InspectorTestProjectContext
{
    public InspectorTestProjectContext(
        Project project,
        FileSystemService fileSystemService,
        TagFileService tagFileService,
        TagDictionaryService tagDictionaryService,
        RecordingClipboardService clipboardService,
        TestStatePersistenceService statePersistenceService,
        StrongReferenceMessenger messenger,
        string catImagePath,
        string dogImagePath)
    {
        Project = project;
        FileSystemService = fileSystemService;
        TagFileService = tagFileService;
        TagDictionaryService = tagDictionaryService;
        ClipboardService = clipboardService;
        StatePersistenceService = statePersistenceService;
        Messenger = messenger;
        CatImagePath = catImagePath;
        DogImagePath = dogImagePath;
    }

    public Project Project { get; }

    public FileSystemService FileSystemService { get; }

    public TagFileService TagFileService { get; }

    public TagDictionaryService TagDictionaryService { get; }

    public RecordingClipboardService ClipboardService { get; }

    public TestStatePersistenceService StatePersistenceService { get; }

    public StrongReferenceMessenger Messenger { get; }

    public string CatImagePath { get; }

    public string DogImagePath { get; }
}
