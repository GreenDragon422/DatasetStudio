# Design Document: DatasetStudio

## Overview

DatasetStudio is a keyboard-first Avalonia/XAML (C#, .NET 10) desktop application for curating and tagging image datasets used in AI training. The application follows the MVVM (Model-View-ViewModel) pattern native to Avalonia, with a service layer handling file system operations, AI model integration, and tag parsing. The visual identity uses the Gruvbox Light palette with IBM Plex Sans/Mono typography and a retro-utilitarian aesthetic with hard 2px border radiuses.

The application consists of five screens: Projects Hub (entry point), Library Grid (batch operations), Inspector Mode (sequential tagging), Project Configuration (modal settings), and Global Tag Dictionary (taxonomy management). All screens share a persistent Hint Bar for keyboard shortcuts. Data is stored entirely on the local file system — projects map to physical folders, tags live in Booru-style `.txt` sidecar files, and configuration is persisted as JSON.

## Architecture

### MVVM Pattern

DatasetStudio uses Avalonia's native MVVM architecture with CommunityToolkit.Mvvm for source-generated observable properties and relay commands.

**Strict MVVM Rules:**
- All observable properties MUST use `[ObservableProperty]` attribute for source generation. Manual `INotifyPropertyChanged` is prohibited where the attribute applies.
- Data flow is strictly: Model → ViewModel → View for updates, View → ViewModel → Model for commands. Models NEVER interact with Views.
- All service operations performing I/O (file system, AI inference, JSON) MUST be `async Task`. No synchronous blocking calls on the UI thread.
- View code-behind contains ONLY view-specific logic (focus management, animation triggers). All business logic lives in ViewModels and Services.

```
┌─────────────────────────────────────────────────────────┐
│                      Views (XAML)                        │
│  ProjectsHubView │ LibraryGridView │ InspectorModeView  │
│  ProjectConfigView │ TagDictionaryView                   │
├─────────────────────────────────────────────────────────┤
│                   ViewModels (C#)                        │
│  ProjectsHubVM │ LibraryGridVM │ InspectorModeVM         │
│  ProjectConfigVM │ TagDictionaryVM │ MainWindowVM         │
├─────────────────────────────────────────────────────────┤
│                   Services (C#)                          │
│  IFileSystemService │ ITagFileService │ IAiTaggerService  │
│  IProjectService │ ITagDictionaryService                 │
│  IKeyboardRouter │ INavigationService │ IClipboardService │
│  IStatePersistenceService │ IThumbnailCacheService        │
├─────────────────────────────────────────────────────────┤
│                   Models (C#)                            │
│  Project │ ImageEntry │ Tag │ WorkflowStage              │
│  TagDictionaryEntry │ ProjectConfiguration               │
└─────────────────────────────────────────────────────────┘
```

### Dependency Injection

Services are registered in a DI container (Microsoft.Extensions.DependencyInjection) at application startup and injected into ViewModels via constructor injection.

### Navigation

Screen navigation is managed by a `NavigationService` that swaps the `ContentControl` in `MainWindow`. The active ViewModel is exposed as `MainWindowVM.CurrentView`. The Project Configuration modal is rendered as an overlay `Panel` within `MainWindow`, toggled by `MainWindowVM.IsConfigOpen`.

```mermaid
graph LR
    A[ProjectsHub] -->|Click Project Card| B[LibraryGrid]
    B -->|Double-click Thumbnail| C[InspectorMode]
    C -->|Back button| B
    B -->|Tag Dictionary nav| D[TagDictionary]
    A -->|New Project| E[ProjectConfig Modal]
    B -->|Config button| E
```

## Event-Driven Architecture

All cross-component communication MUST flow through the CommunityToolkit.Mvvm `IMessenger`. This is a hard architectural rule — ViewModels never call methods on other ViewModels directly.

**Screen-Agnostic Messages** — All messages are general-purpose and contain no screen-specific logic:

```csharp
// Image lifecycle
public record ImageMovedMessage(string ImagePath, string SourceFolder, string TargetFolder);
public record ImageDeletedMessage(string ImagePath, string FolderPath);
public record ImageSelectionChangedMessage(string ImagePath, bool IsSelected);

// Tag lifecycle
public record TagsChangedMessage(string ImagePath, IReadOnlyList<string> NewTags);
public record TagDictionaryChangedMessage(string ProjectId);

// Workflow
public record WorkflowStageChangedMessage(string ProjectId, string FolderPath);
public record ProjectOpenedMessage(string ProjectId);

// AI
public record AiTaggingCompletedMessage(string ImagePath, IReadOnlyList<string> GeneratedTags);

// State
public record ProjectConfigSavedMessage(string ProjectId);
```

**Side Effects Rule** — All side effects (refreshing folder counts, updating status dots, recalculating tag frequencies, updating progress bars) MUST occur as reactions to messenger events or through Avalonia data binding. Side effects MUST NEVER be performed by directly manipulating View elements from code-behind or ViewModel logic. When a ViewModel receives a message, it updates its own observable properties, and the View reacts through binding. This is the ONLY permitted pattern.

**Example flow:**
1. `LibraryGridVM` moves an image → publishes `ImageMovedMessage`
2. `LibraryGridVM` (sidebar section) subscribes → updates folder counts via its own `[ObservableProperty]`
3. `InspectorModeVM` subscribes → refreshes if the moved image was current
4. The View updates automatically through Avalonia data binding — no imperative View manipulation

## Components and Interfaces

### Screen Component Hierarchy

> **Reference Sketches:** Conceptual screen layouts are available in `design reference/*/screen.png`. These are rough sketches showing the general layout idea only — colors are dark-themed and do not reflect the final Gruvbox Light palette, and some visual elements may be missing or incomplete. The implementation must produce fully working screens based on the requirements and component hierarchies below, not pixel-perfect reproductions of the sketches.

#### MainWindow

```
MainWindow
├── TopBar (64px, shared across screens)
│   ├── AppLogo + Title ("DatasetStudio")
│   └── Screen-specific controls (injected per ViewModel)
├── ContentArea (swapped by NavigationService)
│   ├── ProjectsHubView
│   ├── LibraryGridView
│   ├── InspectorModeView
│   └── TagDictionaryView
├── ProjectConfigOverlay (modal, toggled visibility)
└── HintBar (24px, context-sensitive keyboard shortcuts)
```

#### ProjectsHubView
```
ProjectsHubView
├── TopBar
│   ├── MasterRootDirectoryPicker (TextBox + Browse Button)
│   └── NewProjectButton
├── ProjectCardGrid (ItemsControl with WrapPanel)
│   ├── ProjectCard[] (name, path, image count, progress bar)
│   └── EmptyStatePlaceholder (dashed border, centered text)
└── HintBar
```

#### LibraryGridView
```
LibraryGridView
├── TopBar (64px)
│   ├── ProjectName (TextBlock, 18px IBM Plex Sans 600)
│   ├── AiModelDropdown (ComboBox)
│   └── QuickFilterBar (TextBox, IBM Plex Mono)
├── ThreeColumnLayout
│   ├── LeftSidebar (240px)
│   │   └── WorkflowStageList (ListBox of folder items with image counts)
│   ├── CenterGrid (fluid)
│   │   ├── GridToolbar (folder name, item count, status legend)
│   │   ├── ImageGrid (ItemsControl with WrapPanel, min 160px cells)
│   │   │   └── ThumbnailItem[] (1:1 crop, StatusDot, hover checkbox)
│   │   ├── EmptyFolderPlaceholder
│   │   └── ZoomSlider (Slider, 100-400px, bottom-right)
│   └── BatchPopups (Popup controls)
│       ├── BatchAddPopup (autocomplete, triggered by '+')
│       └── BatchRemovePopup (tag list with frequencies, triggered by '-')
├── HintBar
└── StatusBar (feedback messages, contextual info)
```

#### InspectorModeView
```
InspectorModeView
├── TopBar (64px, back button + image identifier + status badge)
├── ThreeColumnLayout
│   ├── LeftSidebar (240px, WorkflowStageList — shared component)
│   ├── CenterPane (fluid)
│   │   ├── ImageViewer (Viewbox, preserves aspect ratio)
│   │   ├── PrevImageButton (overlay, left)
│   │   └── NextImageButton (overlay, right)
│   └── RightSidebar (320px)
│       ├── PrefixTagsBlock (read-only, IBM Plex Mono, Surface Elevated bg)
│       ├── TagInput (32px TextBox, persistent focus, auto-suggest Popup)
│       ├── AppliedTagsList (WrapPanel of TagPill controls)
│       └── CommitButton ("Commit & Next")
├── HintBar
└── StatusBar
```

#### TagDictionaryView
```
TagDictionaryView
├── TopBar (search/filter bar + "New Tag" button)
├── TwoColumnLayout
│   ├── LeftSidebar (240px)
│   │   └── CategoryFilterList (All Tags, Needs Alias, Orphaned, Frequent)
│   └── CenterPane (fluid)
│       └── TagDataGrid (DataGrid with sortable columns)
│           ├── Column: Tag Name (IBM Plex Mono)
│           ├── Column: Alias
│           ├── Column: Global Frequency
│           └── Column: Actions (Edit, Merge, Delete)
├── HintBar
└── StatusBar
```

### Shared Components

| Component | Description |
|---|---|
| `WorkflowStageList` | Reusable ListBox showing workflow folders with stripped numeric prefixes and image counts. Used in LibraryGrid and InspectorMode sidebars. |
| `HintBar` | 24px-height bar displaying context-sensitive keyboard shortcuts in monospace. Content bound to current ViewModel's `HintText` property. |
| `StatusBar` | 24px-height bar at the bottom of every screen for displaying feedback messages and contextual status information. Display only, no input. |
| `TagPill` | Inline control: `Border` containing tag text (IBM Plex Mono) + `x` remove button. Background `#EBDBB2`, border `1px solid #D5C4A1`. |
| `StatusDot` | 12px circle overlay on thumbnails. Color bound to `ImageEntry.TagStatus` enum (Red/Yellow/Green). |
| `ActiveFocusFrame` | 2px solid Warning `#D79921` border applied to the currently focused/active item. Used on thumbnails in Library Grid and image preview in Inspector Mode. Bound to `IsFocused` or `IsActive` property on the ViewModel item. |
| `BatchPopup` | Popup with TextBox + ListBox for autocomplete tag selection. Shared template, parameterized for add vs. remove mode. |

### Service Interfaces

```csharp
public interface IFileSystemService
{
    Task<IReadOnlyList<string>> DiscoverProjectFoldersAsync(string masterRootPath);
    Task<IReadOnlyList<string>> GetImageFilesAsync(string folderPath);
    Task MoveFileAsync(string sourcePath, string destinationFolder);
    Task RecycleFileAsync(string filePath); // Send to OS recycle bin
    Task EnsureFolderExistsAsync(string folderPath);
    // Watches project root for external changes (files added/removed/renamed outside the app).
    // Used to auto-refresh image lists and folder structure when changes are detected.
    FileSystemWatcher WatchFolder(string folderPath);
}

public interface IThumbnailCacheService
{
    Task<Stream> GetThumbnailAsync(string imageFilePath, int size);
    Task InvalidateAsync(string imageFilePath);
    Task InvalidateFolderAsync(string folderPath);
}

public interface ITagFileService
{
    Task<IReadOnlyList<string>> ReadTagsAsync(string tagFilePath);
    Task WriteTagsAsync(string tagFilePath, IReadOnlyList<string> tags);
    Task<IReadOnlyList<string>> ReadTagsWithPrefixAsync(string tagFilePath, IReadOnlyList<string> prefixTags);
    string GetTagFilePath(string imageFilePath); // same base name, .txt extension
    bool TagFileExists(string imageFilePath);
}

public interface IAiTaggerService
{
    Task<IReadOnlyList<string>> GenerateTagsAsync(string imageFilePath, string modelName);
    Task<IReadOnlyList<AiModelInfo>> GetAvailableModelsAsync();
    bool IsProcessing(string imageFilePath);
    event EventHandler<TagGenerationCompletedEventArgs> TagGenerationCompleted;
}

public interface IProjectService
{
    Task<IReadOnlyList<Project>> LoadProjectsAsync();
    Task<Project> CreateProjectAsync(string name, string rootFolder);
    Task SaveProjectAsync(Project project);
    Task DeleteProjectAsync(string projectId);
}

public interface ITagDictionaryService
{
    Task<IReadOnlyList<TagDictionaryEntry>> GetAllEntriesAsync(string projectId);
    Task<IReadOnlyList<string>> SearchTagsAsync(string projectId, string query);
    Task RenameTagAsync(string projectId, string oldName, string newName);
    Task MergeTagsAsync(string projectId, string sourceTag, string targetTag);
    Task DeleteTagAsync(string projectId, string tagName, bool removeFromFiles);
    Task AddAliasAsync(string projectId, string canonicalTag, string alias);
    string ResolveAlias(string projectId, string input);
}

public interface INavigationService
{
    void NavigateTo<TViewModel>() where TViewModel : ViewModelBase;
    void NavigateTo<TViewModel>(object parameter) where TViewModel : ViewModelBase;
    void GoBack();
}

public interface IClipboardService
{
    Task CopyTagsAsync(IReadOnlyList<string> tags);
    Task<IReadOnlyList<string>> PasteTagsAsync();
}

public interface IStatePersistenceService
{
    Task SaveAppStateAsync(AppState state);
    Task<AppState> LoadAppStateAsync();
    Task SaveProjectStateAsync(string projectId, ProjectState state);
    Task<ProjectState> LoadProjectStateAsync(string projectId);
}
```

### Keyboard Input Routing Architecture

Keyboard input is handled at two levels:

1. **Global KeyBindings** — Registered on `MainWindow` via Avalonia `KeyBinding` elements. These handle cross-screen shortcuts like `Ctrl+Shift+C`/`V` (tag copy/paste) and `/` (focus filter). The `MainWindowVM` delegates to the active child ViewModel.

2. **Screen-level InputBindings** — Each View registers its own `KeyDown` handler that delegates to the ViewModel. The ViewModel exposes `ICommand` properties for each shortcut action.

```mermaid
graph TD
    A[MainWindow.KeyDown] --> B{Is global shortcut?}
    B -->|Yes| C[MainWindowVM handles]
    B -->|No| D[Propagate to active View]
    D --> E[View.KeyDown handler]
    E --> F{Is text input focused?}
    F -->|Yes, tag input| G[Let TextBox handle normally]
    F -->|No| H[ViewModel.HandleKeyCommand]
    H --> I[Execute bound ICommand]
```

Key routing rules:
- When a `TextBox` has focus (tag input, filter bar), letter keys are consumed by the TextBox. `Escape` returns focus to the parent container (grid or image viewer).
- `Escape` dismisses any open popup (batch add/remove). If no popup is open in Inspector Mode, `Escape` navigates back to Library Grid.
- In Library Grid, when no TextBox is focused: arrow keys navigate the grid, `x` toggles selection, `+`/`-` open batch popups, `[`/`]` move images, `Delete` recycles images.
- In Inspector Mode, any letter key auto-focuses the tag input (requirement 3.6). Arrow keys navigate images. `[`/`]` move the current image between stages. `Delete` recycles the current image.
- The `HintBar` content updates reactively based on `CurrentScreen` and `IsTextInputFocused` state.

## Data Models

### Core Models

```csharp
public class Project
{
    public string Id { get; set; }              // GUID
    public string Name { get; set; }            // Display name (subfolder name or user-defined)
    public string RootFolderPath { get; set; }  // Absolute path to project root
    public List<WorkflowStage> Stages { get; set; }
    public List<string> PrefixTags { get; set; }
    public string AiModelName { get; set; }     // Selected AI model identifier
    public DateTime LastModified { get; set; }
}

public class WorkflowStage
{
    public int Order { get; set; }              // Parsed from numeric prefix (e.g., "01_Inbox" → 1)
    public string FolderName { get; set; }      // Physical folder name on disk (e.g., "01_Inbox")
    public string DisplayName { get; set; }     // Stripped name (e.g., "Inbox")
}
```

> **Dynamic Folder Ordering:** Workflow stage order is ALWAYS derived at runtime by parsing the numeric prefix from each subfolder name on disk (e.g., `01_` → order 1, `02_` → order 2). The application MUST NOT hardcode or cache folder ordering. This ensures stages can be reordered, added, or removed by simply renaming folders on disk, without requiring code changes. When loading a project, the application scans the root folder, extracts numeric prefixes, sorts by that value, and builds the `WorkflowStage` list dynamically.

```csharp
public class ImageEntry
{
    public string FilePath { get; set; }        // Absolute path to image file
    public string FileName { get; set; }        // File name without extension
    public string TagFilePath { get; set; }     // Companion .txt file path
    public TagStatus Status { get; set; }       // Red, Yellow, Green
    public List<string> Tags { get; set; }      // Current tag list (excluding prefix)
    public bool IsSelected { get; set; }        // Selection state for batch ops
    public bool IsAiProcessing { get; set; }    // True while AI tagger is running
}

public enum TagStatus
{
    Untagged,       // Red — no tag file exists
    AutoTagged,     // Yellow — AI-generated, needs human review
    Ready           // Green — human-reviewed, ready for training
}

public class TagDictionaryEntry
{
    public string CanonicalName { get; set; }   // The primary tag name
    public List<string> Aliases { get; set; }   // Alternative names that resolve to this tag
    public int GlobalFrequency { get; set; }    // Usage count across all project images
}

public class AiModelInfo
{
    public string Id { get; set; }
    public string DisplayName { get; set; }
    public string ModelPath { get; set; }
}
```

### On-Disk Data Layout

```
MasterRootDirectory/
├── ProjectA/                          ← Project.RootFolderPath
│   ├── .datasetstudio.json            ← Project configuration file
│   ├── .datasetstudio-cache/          ← Thumbnail cache (auto-generated)
│   ├── 01_Inbox/                      ← WorkflowStage folder
│   │   ├── image001.png
│   │   ├── image001.txt               ← Tag file: "tag1, tag2, tag3"
│   │   ├── image002.jpg
│   │   └── image002.txt
│   ├── 02_Review/
│   │   └── ...
│   └── 03_Ready/
│       └── ...
├── ProjectB/
│   ├── .datasetstudio.json
│   └── ...
└── ai_models.json                     ← AI model registry (shared)
```

### Tag File Format

Each `.txt` file stores tags as a single line of comma-separated values:

```
tag1, tag2, tag3, another tag
```

When saving, prefix tags are prepended:

```
prefix1, prefix2, tag1, tag2, tag3
```

### Project Configuration File (`.datasetstudio.json`)

```json
{
  "id": "a1b2c3d4-...",
  "name": "Cyberpunk_Cityscapes",
  "stages": [
    { "order": 0, "folderName": "01_Inbox", "displayName": "Inbox" },
    { "order": 1, "folderName": "02_Review", "displayName": "Review" },
    { "order": 2, "folderName": "03_Ready", "displayName": "Ready" }
  ],
  "prefixTags": ["lora_style", "masterpiece", "best quality"],
  "aiModelName": "wd14-vit-v2",
  "state": {
    "activeStageFolderName": "01_Inbox",
    "zoomSliderValue": 160,
    "selectedAiModelName": "wd14-vit-v2",
    "lastInspectedImagePath": null
  }
}
```

### State Management

Each ViewModel holds its own observable state using `CommunityToolkit.Mvvm` `[ObservableProperty]` attributes. Cross-ViewModel communication uses the `IMessenger` (from CommunityToolkit.Mvvm) exclusively — see the Event-Driven Architecture section above for the full message catalog.

### State Persistence

All configurable settings and stateful properties are persisted automatically so the user resumes exactly where they left off on next launch.

**Application-level state** (`datasetstudio-settings.json` in user app data):
```csharp
public class AppState
{
    public string? LastOpenedProjectId { get; set; }
    public double WindowWidth { get; set; }
    public double WindowHeight { get; set; }
    public double WindowX { get; set; }
    public double WindowY { get; set; }
    public string? LastMasterRootDirectory { get; set; }
}
```

**Project-level state** (stored within `.datasetstudio.json`):
```csharp
public class ProjectState
{
    public string? ActiveStageFolderName { get; set; }
    public int ZoomSliderValue { get; set; }
    public string? SelectedAiModelName { get; set; }
    public string? LastInspectedImagePath { get; set; }
}
```

Every property that can be configured or have a state MUST be included in persistence. When adding new stateful properties to any ViewModel, the corresponding persistence model MUST be updated to include them.

State is saved on change (debounced to avoid excessive writes) and loaded at startup before the first View is rendered.

### Supported Image Formats

- Read: PNG (`.png`), JPEG (`.jpg`, `.jpeg`), WebP (`.webp`), BMP (`.bmp`)
- Thumbnails: saved as WebP (`.webp`) in the cache for optimal size

Source images are never converted — they remain in their original format on disk.

The `IFileSystemService.GetImageFilesAsync` filters by these extensions. Any file with a recognized extension in a Workflow_Stage folder is treated as a valid image entry.

### Caching Architecture

**Thumbnail Cache** — Thumbnails are generated on first access and stored in a `.datasetstudio-cache/` subfolder within each project's root folder. Cache keys are derived from the image file path and modification timestamp. When the source image changes (timestamp mismatch), the cached thumbnail is invalidated and regenerated on next access.

```
ProjectA/
├── .datasetstudio-cache/
│   ├── 01_Inbox/
│   │   ├── image001_160.webp    ← cached thumbnail at 160px
│   │   └── image002_160.webp
│   └── 02_Review/
│       └── ...
├── 01_Inbox/
│   ├── image001.png
│   └── ...
```

**In-Memory Caches:**
- Tag Dictionary — loaded once per project open, refreshed on `TagDictionaryChangedMessage` events. Used for autocomplete in Inspector Mode and batch popups.
- Workflow Stage list — parsed from disk on project open, refreshed when `FileSystemWatcher` detects folder structure changes (add/remove/rename subfolders).
- Image file lists per folder — loaded on folder selection, refreshed on `FileSystemWatcher` file events or after move/delete operations.

All cache invalidation flows through the messenger event system — when an `ImageMovedMessage`, `ImageDeletedMessage`, or `TagsChangedMessage` is published, the relevant caches update reactively.

### Data Flow: Tag Commit (Inspector Mode)

```mermaid
sequenceDiagram
    participant User
    participant InspectorVM
    participant TagFileService
    participant Messenger
    participant DictionaryService

    User->>InspectorVM: Press Enter with tag text
    InspectorVM->>InspectorVM: Validate tag (non-empty, resolve alias)
    InspectorVM->>InspectorVM: Add tag to ImageEntry.Tags
    InspectorVM->>TagFileService: WriteTagsAsync(tagFilePath, tags)
    TagFileService->>TagFileService: Serialize to "prefix, tag1, tag2, ..."
    TagFileService-->>InspectorVM: Write complete
    InspectorVM->>Messenger: Send TagsChangedMessage
    Messenger->>DictionaryService: Update frequency counts
    InspectorVM->>InspectorVM: Auto-advance to next untagged image (Red or Yellow)
    InspectorVM->>InspectorVM: Clear tag input, restore focus
```

### Data Flow: Image Move Between Stages

```mermaid
sequenceDiagram
    participant User
    participant LibraryGridVM
    participant FileSystemService
    participant TagFileService
    participant Messenger

    User->>LibraryGridVM: Press ']' (move to next stage)
    LibraryGridVM->>LibraryGridVM: Determine target stage from current + 1
    LibraryGridVM->>FileSystemService: MoveFileAsync(image, targetFolder)
    LibraryGridVM->>FileSystemService: MoveFileAsync(tagFile, targetFolder)
    FileSystemService-->>LibraryGridVM: Move complete
    LibraryGridVM->>LibraryGridVM: Remove image from current folder list
    LibraryGridVM->>Messenger: Send ImageMovedMessage
    Messenger->>LibraryGridVM: Refresh folder counts in sidebar
```

### Data Flow: Delete to Recycle Bin

```mermaid
sequenceDiagram
    participant User
    participant ViewModel
    participant FileSystemService
    participant Messenger

    User->>ViewModel: Press Delete key
    ViewModel->>ViewModel: Get target image(s) (selected in Grid, current in Inspector)
    loop For each target image
        ViewModel->>FileSystemService: RecycleFileAsync(imagePath)
        ViewModel->>FileSystemService: RecycleFileAsync(tagFilePath)
    end
    FileSystemService-->>ViewModel: Recycle complete
    ViewModel->>Messenger: Send ImageDeletedMessage(imagePath, folderPath)
    ViewModel->>ViewModel: Auto-advance focus to next image
    Note over Messenger: All subscribers react independently:<br/>Folder counts update, grid refreshes,<br/>tag dictionary recalculates
```

### Data Flow: Batch Tag Add

```mermaid
sequenceDiagram
    participant User
    participant LibraryGridVM
    participant TagFileService
    participant Messenger

    User->>LibraryGridVM: Press '+', type tag, press Enter
    LibraryGridVM->>LibraryGridVM: Resolve alias via DictionaryService
    LibraryGridVM->>LibraryGridVM: Get target images (selection or full folder)
    loop For each target image
        LibraryGridVM->>TagFileService: ReadTagsAsync(tagFilePath)
        TagFileService-->>LibraryGridVM: existing tags
        LibraryGridVM->>LibraryGridVM: Skip if tag already present (dedup)
        LibraryGridVM->>TagFileService: WriteTagsAsync(tagFilePath, updatedTags)
    end
    LibraryGridVM->>Messenger: Send TagsChangedMessage (batch)
    LibraryGridVM->>LibraryGridVM: Close popup, update status dots
```


## Error Handling

### File System Errors

| Scenario | Handling |
|---|---|
| Root folder path does not exist | Display error in StatusBar. Disable project loading. Prompt user to reconfigure via Project Configuration. |
| Image file locked/inaccessible during move | Retry once after 500ms. If still locked, display error in StatusBar with file path. Skip the file and continue batch operation. |
| Recycle bin operation fails (file locked, recycle bin full) | Display error in StatusBar with file path. Do not advance focus. Leave image in place. |
| Tag file write fails (permissions, disk full) | Display error in StatusBar. Revert in-memory tag state to last known good state. |
| Tag file missing during read | Treat image as untagged (Red status). Queue for AI tagging if AI tagger is enabled. |
| Master root directory scan fails | Display error in StatusBar. Show empty Projects Hub with placeholder text. |

### AI Tagger Errors

| Scenario | Handling |
|---|---|
| AI model file not found | Display warning in StatusBar. Disable AI tagger for the session. Allow manual tagging. |
| AI model inference fails for an image | Log error. Set image status to Untagged (Red). Skip to next image in queue. Display count of failed images in StatusBar. |
| AI model JSON config malformed | Fall back to empty model list. Display error in StatusBar. |

### Data Validation Errors

| Scenario | Handling |
|---|---|
| Prefix tags contain invalid characters | Show red border on textarea + error message below field (requirement 4.5). Prevent save until corrected. |
| Duplicate tag addition | Silently skip the duplicate. No error displayed. |
| Tag merge creates circular alias | Detect cycle before merge. Display error in StatusBar. Abort merge operation. |
| Empty tag input on Enter | Ignore the keypress. Do not commit an empty tag. |

### Graceful Degradation

- If the AI tagger service is unavailable, the application operates fully in manual mode. All tagging features work; only auto-tagging is disabled.
- If a tag file is corrupted (non-parseable), treat the image as untagged and log a warning. Do not overwrite the corrupted file until the user explicitly saves new tags.
- If the `.datasetstudio.json` config file is missing from a project folder, create a default configuration using the folder name as the project name and auto-detecting subfolders as workflow stages.


## Testing Strategy

### Philosophy

Tests serve two purposes in DatasetStudio:

1. **Regression detection** — Catch when an LLM or manual edit breaks core low-level behaviors (config serialization, tag parsing, file operations, state persistence).
2. **TDD guardrails** — Force service logic to be implemented in testable, decoupled classes rather than glued directly into Views or ViewModels. If a behavior can't be tested without spinning up a UI, it's in the wrong layer.

All tests use **NUnit**. No property-based testing libraries. Keep it simple — focused assertions on concrete scenarios.

### What to Test

Tests cover the service layer and pure logic only. No UI tests, no ViewModel tests that require Avalonia runtime.

| Test Class | What It Covers |
|---|---|
| `TagFileServiceTests` | Read/write tag files (round-trip), comma parsing, prefix prepending, path derivation (.txt from image path), empty file handling, whitespace trimming |
| `ProjectConfigServiceTests` | Save/load `.datasetstudio.json` round-trip, all fields preserved (stages, prefix tags, AI model, state block), malformed JSON fallback |
| `StatePersistenceTests` | App state save/load round-trip (window geometry, last project, last master root), project state save/load (active stage, zoom, last inspected image) |
| `WorkflowStageTests` | Numeric prefix parsing and ordering (`03_Ready`, `01_Inbox`, `02_Review` → sorted correctly), display name stripping, folders without prefixes sorted after numbered ones |
| `TagDictionaryServiceTests` | Add/remove/rename tags, alias resolution, merge updates all references, frequency counting |
| `FileOperationTests` | Move image + tag file together, recycle both files, verify source gone and target exists |
| `BatchOperationTests` | Batch add skips duplicates, batch remove eliminates tag, all other tags preserved |
| `ThumbnailCacheTests` | Cache miss generates thumbnail, cache hit returns cached file, stale cache invalidated on timestamp change |

### Test Organization

```
DatasetStudio.Tests/
├── TagFileServiceTests.cs
├── ProjectConfigServiceTests.cs
├── StatePersistenceTests.cs
├── WorkflowStageTests.cs
├── TagDictionaryServiceTests.cs
├── FileOperationTests.cs
├── BatchOperationTests.cs
└── ThumbnailCacheTests.cs
```

### TDD Rule

When implementing a new service method or behavior, the test MUST be written first. The implementation is then written to make the test pass. This ensures all service logic is exercised through the public interface and never coupled to UI concerns.
