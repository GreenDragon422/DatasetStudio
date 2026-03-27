# Implementation Plan: DatasetStudio

## Overview

Build a keyboard-first Avalonia/XAML (C#, .NET 10) desktop application for curating and tagging image datasets. Implementation proceeds bottom-up in four phases:

- **Phase 1 — Foundation (Single Agent):** Solution scaffold, design tokens, models, interfaces, DI, navigation, MainWindow shell. Must complete before anything else.
- **Phase 2 — Core Services (Single Agent):** All service implementations with TDD tests. Builds on Phase 1 infra. Must complete before screens.
- **Phase 3 — Screens (Parallel Sub-Agents):** Each screen (ViewModel + View) is independent and can be built in parallel once Phase 2 is done.
- **Phase 4 — Integration Wiring (Single Agent):** Cross-cutting concerns — keyboard routing, state persistence hookup, AI tagger wiring, FileSystemWatcher.

## Phase 1 — Foundation (Single Agent, Sequential)

- [ ] 1. Create solution and project files
  - [ ] 1.1 Create `DatasetStudio.sln` with two projects: `DatasetStudio` (Avalonia app, net10.0) and `DatasetStudio.Tests` (NUnit class library, net10.0)
  - [ ] 1.2 Add NuGet references to `DatasetStudio.csproj`: Avalonia, Avalonia.Desktop, Avalonia.Themes.Fluent, CommunityToolkit.Mvvm, Microsoft.Extensions.DependencyInjection
  - [ ] 1.3 Add NuGet references to `DatasetStudio.Tests.csproj`: NUnit, NUnit3TestAdapter, Microsoft.NET.Test.Sdk, project reference to DatasetStudio
  - [ ] 1.4 Create folder structure: `Models/`, `ViewModels/`, `Views/`, `Services/`, `Messages/`, `Controls/`, `Resources/`
  - _Requirements: 13.1, 13.3_

- [ ] 2. Create XAML design system resources
  - [ ] 2.1 Create `Resources/Colors.axaml` — Gruvbox Light palette: Background `#FBF1C7`, Surface `#EBDBB2`, Surface Elevated `#D5C4A1`, Primary `#D65D0E`, Text `#3C3836`, Muted `#7C6F64`, Accent `#98971A`, Warning `#D79921`, Error `#CC241D`
  - [ ] 2.2 Create `Resources/Typography.axaml` — IBM Plex Sans (headings 600/18-24px, body 400/13px, buttons 500/12px uppercase 0.5px tracking), IBM Plex Mono (tags/metadata 500/12px)
  - [ ] 2.3 Create `Resources/Styles.axaml` — spacing tokens (4px, 8px, 16px, 24px), 2px border radius, 1px solid borders, ActiveFocusFrame style (2px solid Warning `#D79921`)
  - [ ] 2.4 Merge all ResourceDictionaries into `App.axaml`
  - _Requirements: 10.1, 10.2, 10.3, 10.4, 10.5_

- [ ] 3. Create core data models
  - [ ] 3.1 Create `Models/Project.cs` — Id (string GUID), Name, RootFolderPath, Stages (List<WorkflowStage>), PrefixTags (List<string>), AiModelName, LastModified
  - [ ] 3.2 Create `Models/WorkflowStage.cs` — Order (int), FolderName (string), DisplayName (string)
  - [ ] 3.3 Create `Models/ImageEntry.cs` — FilePath, FileName, TagFilePath, Status (TagStatus), Tags (List<string>), IsSelected, IsAiProcessing
  - [ ] 3.4 Create `Models/TagStatus.cs` enum — Untagged, AutoTagged, Ready
  - [ ] 3.5 Create `Models/TagDictionaryEntry.cs` — CanonicalName, Aliases (List<string>), GlobalFrequency (int)
  - [ ] 3.6 Create `Models/AiModelInfo.cs` — Id, DisplayName, ModelPath
  - [ ] 3.7 Create `Models/AppState.cs` — LastOpenedProjectId, WindowWidth, WindowHeight, WindowX, WindowY, LastMasterRootDirectory
  - [ ] 3.8 Create `Models/ProjectState.cs` — ActiveStageFolderName, ZoomSliderValue, SelectedAiModelName, LastInspectedImagePath
  - _Requirements: 8.2, 6.1, 7.4, 11.1, 11.4_

- [ ] 4. Create IMessenger event messages
  - [ ] 4.1 Create `Messages/ImageMovedMessage.cs` — record(string ImagePath, string SourceFolder, string TargetFolder)
  - [ ] 4.2 Create `Messages/ImageDeletedMessage.cs` — record(string ImagePath, string FolderPath)
  - [ ] 4.3 Create `Messages/ImageSelectionChangedMessage.cs` — record(string ImagePath, bool IsSelected)
  - [ ] 4.4 Create `Messages/TagsChangedMessage.cs` — record(string ImagePath, IReadOnlyList<string> NewTags)
  - [ ] 4.5 Create `Messages/TagDictionaryChangedMessage.cs` — record(string ProjectId)
  - [ ] 4.6 Create `Messages/WorkflowStageChangedMessage.cs` — record(string ProjectId, string FolderPath)
  - [ ] 4.7 Create `Messages/ProjectOpenedMessage.cs` — record(string ProjectId)
  - [ ] 4.8 Create `Messages/AiTaggingCompletedMessage.cs` — record(string ImagePath, IReadOnlyList<string> GeneratedTags)
  - [ ] 4.9 Create `Messages/ProjectConfigSavedMessage.cs` — record(string ProjectId)
  - _Requirements: 12.1, 12.2, 12.3, 12.4_

- [ ] 5. Create service interfaces
  - [ ] 5.1 Create `Services/IFileSystemService.cs` — DiscoverProjectFoldersAsync, GetImageFilesAsync, MoveFileAsync, RecycleFileAsync, EnsureFolderExistsAsync, WatchFolder
  - [ ] 5.2 Create `Services/IThumbnailCacheService.cs` — GetThumbnailAsync, InvalidateAsync, InvalidateFolderAsync
  - [ ] 5.3 Create `Services/ITagFileService.cs` — ReadTagsAsync, WriteTagsAsync, ReadTagsWithPrefixAsync, GetTagFilePath, TagFileExists
  - [ ] 5.4 Create `Services/IAiTaggerService.cs` — GenerateTagsAsync, GetAvailableModelsAsync, IsProcessing, TagGenerationCompleted event
  - [ ] 5.5 Create `Services/IProjectService.cs` — LoadProjectsAsync, CreateProjectAsync, SaveProjectAsync, DeleteProjectAsync
  - [ ] 5.6 Create `Services/ITagDictionaryService.cs` — GetAllEntriesAsync, SearchTagsAsync, RenameTagAsync, MergeTagsAsync, DeleteTagAsync, AddAliasAsync, ResolveAlias
  - [ ] 5.7 Create `Services/INavigationService.cs` — NavigateTo<T>(), NavigateTo<T>(object), GoBack()
  - [ ] 5.8 Create `Services/IClipboardService.cs` — CopyTagsAsync, PasteTagsAsync
  - [ ] 5.9 Create `Services/IStatePersistenceService.cs` — SaveAppStateAsync, LoadAppStateAsync, SaveProjectStateAsync, LoadProjectStateAsync
  - _Requirements: 13.3, 12.1_

- [ ] 6. Create ViewModelBase and MainWindowViewModel
  - [ ] 6.1 Create `ViewModels/ViewModelBase.cs` — abstract class extending ObservableRecipient, with `[ObservableProperty]` for HintText (string) and StatusText (string)
  - [ ] 6.2 Create `ViewModels/MainWindowViewModel.cs` — `[ObservableProperty]` for CurrentView (ViewModelBase), IsConfigOpen (bool), HintText, StatusText. Inject INavigationService and IMessenger.
  - _Requirements: 13.1, 13.2, 9.2, 9.3_

- [ ] 7. Implement NavigationService
  - [ ] 7.1 Create `Services/NavigationService.cs` — implements INavigationService, resolves ViewModels from DI, sets MainWindowViewModel.CurrentView, maintains back stack for GoBack()
  - _Requirements: 1.7, 2.18, 3.15_

- [ ] 8. Create DI container and App startup
  - [ ] 8.1 Create `App.axaml.cs` — build IServiceProvider, register all service interfaces → implementations, register all ViewModels, resolve MainWindowViewModel on startup
  - [ ] 8.2 Create `Program.cs` entry point with Avalonia AppBuilder configuration
  - _Requirements: 12.1, 13.2_

- [ ] 9. Create MainWindow shell
  - [ ] 9.1 Create `Views/MainWindow.axaml` — 64px TopBar with AppLogo + Title ("DatasetStudio") and ContentPresenter for screen-specific controls, ContentControl bound to CurrentView, overlay Panel for ProjectConfig modal (toggled by IsConfigOpen), bottom HintBar (24px) and StatusBar (24px)
  - [ ] 9.2 Create `Views/MainWindow.axaml.cs` code-behind — DataContext assignment, global KeyDown handler stub (delegates to active ViewModel)
  - _Requirements: 9.2, 9.3, 13.4_

- [ ] 10. Create shared UI controls
  - [ ] 10.1 Create `Controls/WorkflowStageList.axaml` — reusable ListBox showing workflow folders with stripped numeric prefixes and image counts, bindable ItemsSource and SelectedItem
  - [ ] 10.2 Create `Controls/HintBar.axaml` — 24px-height bar, IBM Plex Mono, content bound to HintText property
  - [ ] 10.3 Create `Controls/StatusBar.axaml` — 24px-height display-only bar, bound to StatusText property
  - [ ] 10.4 Create `Controls/TagPill.axaml` — Border with tag text (IBM Plex Mono) + `x` remove button, Background `#EBDBB2`, border `1px solid #D5C4A1`, exposes Tag (string) and RemoveCommand
  - [ ] 10.5 Create `Controls/StatusDot.axaml` — 12px circle, color bound to TagStatus enum (Red=#CC241D, Yellow=#D79921, Green=#98971A)
  - [ ] 10.6 Create `Controls/BatchPopup.axaml` — Popup with TextBox + ListBox for autocomplete tag selection, parameterized for add vs. remove mode via Mode property
  - _Requirements: 2.5, 2.23, 3.5, 9.2, 9.3, 10.3, 10.5_

- [ ] 11. Foundation checkpoint
  - Verify solution builds. Verify all interfaces, models, messages, controls, and MainWindow shell compile. Verify design resources load. Ask user if questions arise.


## Phase 2 — Core Services with TDD Tests (Single Agent, Sequential)

- [ ] 12. Implement TagFileService
  - [ ] 12.1 Create `Services/TagFileService.cs` implementing ITagFileService
  - [ ] 12.2 Implement `GetTagFilePath` — derive .txt path from image path (replace extension with .txt)
  - [ ] 12.3 Implement `TagFileExists` — check if companion .txt file exists on disk
  - [ ] 12.4 Implement `ReadTagsAsync` — read .txt file, split by comma, trim whitespace per tag, return list. Return empty list if file missing or empty.
  - [ ] 12.5 Implement `WriteTagsAsync` — join tags with ", " separator, write single line to .txt file
  - [ ] 12.6 Implement `ReadTagsWithPrefixAsync` — call ReadTagsAsync, prepend prefix tags to result
  - _Requirements: 6.1, 6.2, 6.3, 6.4_

- [ ]* 13. Write TagFileService tests
  - [ ]* 13.1 Test `GetTagFilePath` — .png → .txt, .jpg → .txt, .jpeg → .txt, .webp → .txt, .bmp → .txt
  - [ ]* 13.2 Test read/write round-trip — write tags then read back produces identical list
  - [ ]* 13.3 Test comma parsing with whitespace trimming — "tag1 , tag2 ,  tag3 " → ["tag1", "tag2", "tag3"]
  - [ ]* 13.4 Test prefix tag prepending — prefix ["a", "b"] + tags ["c", "d"] → file contains "a, b, c, d"
  - [ ]* 13.5 Test empty file returns empty list
  - [ ]* 13.6 Test missing file returns empty list (no exception)
  - [ ]* 13.7 Test whitespace-only tags are excluded
  - _Requirements: 6.4_

- [ ] 14. Implement WorkflowStage parsing logic
  - [ ] 14.1 Create `Services/WorkflowStageParser.cs` (static helper or service) — parse numeric prefix from folder name (regex `^\d+[_-]`), extract order int and display name
  - [ ] 14.2 Implement sorting: folders with numeric prefixes sorted by prefix value, folders without prefixes sorted alphabetically after numbered ones
  - [ ] 14.3 Implement display name stripping: remove numeric prefix and separator (e.g., "01_Inbox" → "Inbox", "02-Review" → "Review")
  - _Requirements: 8.1, 8.2, 8.4_

- [ ]* 15. Write WorkflowStage parsing tests
  - [ ]* 15.1 Test ordering: ["03_Ready", "01_Inbox", "02_Review"] → sorted as [Inbox(1), Review(2), Ready(3)]
  - [ ]* 15.2 Test display name stripping: "01_Inbox" → "Inbox", "02-Review" → "Review"
  - [ ]* 15.3 Test folders without numeric prefix sorted after numbered ones
  - [ ]* 15.4 Test single-digit and multi-digit prefixes: "1_A", "10_B", "2_C" → [A(1), C(2), B(10)]
  - _Requirements: 8.2_

- [ ] 16. Implement FileSystemService
  - [ ] 16.1 Create `Services/FileSystemService.cs` implementing IFileSystemService
  - [ ] 16.2 Implement `GetImageFilesAsync` — enumerate files in folder, filter by extensions (.png, .jpg, .jpeg, .webp, .bmp), return sorted list
  - [ ] 16.3 Implement `DiscoverProjectFoldersAsync` — scan master root for subfolders containing `.datasetstudio.json`
  - [ ] 16.4 Implement `MoveFileAsync` — File.Move with overwrite protection
  - [ ] 16.5 Implement `RecycleFileAsync` — send to OS recycle bin (use Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile with RecycleOption on Windows)
  - [ ] 16.6 Implement `EnsureFolderExistsAsync` — Directory.CreateDirectory if not exists
  - [ ] 16.7 Implement `WatchFolder` — return configured FileSystemWatcher for project root
  - _Requirements: 8.1, 8.3, 8.5, 14.1, 14.3_

- [ ]* 17. Write FileSystemService tests
  - [ ]* 17.1 Test `GetImageFilesAsync` returns only supported extensions, ignores .txt and other files
  - [ ]* 17.2 Test `MoveFileAsync` — source gone, target exists
  - [ ]* 17.3 Test `EnsureFolderExistsAsync` — creates folder, no error if already exists
  - [ ]* 17.4 Test `DiscoverProjectFoldersAsync` — finds folders with .datasetstudio.json, ignores others
  - _Requirements: 8.3, 14.1_

- [ ] 18. Implement ProjectService
  - [ ] 18.1 Create `Services/ProjectService.cs` implementing IProjectService
  - [ ] 18.2 Implement `LoadProjectsAsync` — scan for `.datasetstudio.json` files in known paths, deserialize with System.Text.Json
  - [ ] 18.3 Implement `CreateProjectAsync` — generate GUID, build default Project with auto-detected stages, write `.datasetstudio.json`
  - [ ] 18.4 Implement `SaveProjectAsync` — serialize Project to `.datasetstudio.json` with indented formatting
  - [ ] 18.5 Implement `DeleteProjectAsync` — remove `.datasetstudio.json` file
  - [ ] 18.6 Handle malformed JSON — catch JsonException, return default Project using folder name as project name
  - _Requirements: 1.3, 4.7, 11.4_

- [ ]* 19. Write ProjectService tests
  - [ ]* 19.1 Test save/load round-trip — all fields preserved (id, name, stages, prefixTags, aiModelName, state block)
  - [ ]* 19.2 Test malformed JSON falls back to default Project
  - [ ]* 19.3 Test CreateProjectAsync generates valid GUID and writes file
  - _Requirements: 11.4_

- [ ] 20. Implement StatePersistenceService
  - [ ] 20.1 Create `Services/StatePersistenceService.cs` implementing IStatePersistenceService
  - [ ] 20.2 Implement `SaveAppStateAsync` / `LoadAppStateAsync` — persist AppState to `datasetstudio-settings.json` in Environment.SpecialFolder.ApplicationData
  - [ ] 20.3 Implement `SaveProjectStateAsync` / `LoadProjectStateAsync` — read/write the `state` block within `.datasetstudio.json`
  - [ ] 20.4 Implement debounced save — use a Timer that resets on each save call, fires after 500ms of inactivity
  - [ ] 20.5 Handle missing files — return default AppState/ProjectState with sensible defaults
  - _Requirements: 11.1, 11.2, 11.3, 11.4_

- [ ]* 21. Write StatePersistenceService tests
  - [ ]* 21.1 Test AppState round-trip — window geometry, last project ID, last master root directory all preserved
  - [ ]* 21.2 Test ProjectState round-trip — active stage, zoom value, selected AI model, last inspected image path all preserved
  - [ ]* 21.3 Test missing settings file returns default AppState
  - [ ]* 21.4 Test missing project state returns default ProjectState
  - _Requirements: 11.1, 11.2_

- [ ] 22. Implement ThumbnailCacheService
  - [ ] 22.1 Create `Services/ThumbnailCacheService.cs` implementing IThumbnailCacheService
  - [ ] 22.2 Implement `GetThumbnailAsync` — compute cache path from image path + size, check if cached file exists and source timestamp matches, return cached stream on hit
  - [ ] 22.3 Implement cache miss path — load source image, resize to requested size (square crop), encode as WebP, write to `.datasetstudio-cache/` subfolder, return stream
  - [ ] 22.4 Implement `InvalidateAsync` — delete cached thumbnail for a single image
  - [ ] 22.5 Implement `InvalidateFolderAsync` — delete all cached thumbnails in a folder's cache directory
  - _Requirements: 15.1, 15.2, 15.3, 14.2_

- [ ]* 23. Write ThumbnailCacheService tests
  - [ ]* 23.1 Test cache miss generates thumbnail file in correct cache path
  - [ ]* 23.2 Test cache hit returns existing file without regenerating
  - [ ]* 23.3 Test stale cache — when source timestamp changes, old cache is invalidated and new thumbnail generated
  - [ ]* 23.4 Test InvalidateAsync removes the cached file
  - _Requirements: 15.1, 15.2, 15.3_

- [ ] 24. Implement TagDictionaryService
  - [ ] 24.1 Create `Services/TagDictionaryService.cs` implementing ITagDictionaryService
  - [ ] 24.2 Implement `GetAllEntriesAsync` — scan all tag files in project, build frequency map, return TagDictionaryEntry list
  - [ ] 24.3 Implement `SearchTagsAsync` — filter entries by substring match on canonical name and aliases
  - [ ] 24.4 Implement `AddAliasAsync` — add alias mapping to a canonical tag entry
  - [ ] 24.5 Implement `ResolveAlias` — given input string, return canonical tag name if alias exists, otherwise return input unchanged
  - [ ] 24.6 Implement `RenameTagAsync` — rename tag in dictionary and update all tag files that contain it
  - [ ] 24.7 Implement `MergeTagsAsync` — merge source tag into target, update all tag file references, detect circular alias before merge
  - [ ] 24.8 Implement `DeleteTagAsync` — remove from dictionary, optionally scan and remove from all tag files
  - [ ] 24.9 Implement in-memory cache — load dictionary once per project open, subscribe to TagDictionaryChangedMessage for refresh
  - _Requirements: 5.3, 5.4, 5.5, 5.6, 15.4_

- [ ]* 25. Write TagDictionaryService tests
  - [ ]* 25.1 Test alias resolution — alias "cat" → canonical "feline" returns "feline"
  - [ ]* 25.2 Test unknown alias returns input unchanged
  - [ ]* 25.3 Test RenameTagAsync updates tag in all files
  - [ ]* 25.4 Test MergeTagsAsync merges source into target across all files
  - [ ]* 25.5 Test circular alias detection — merging A→B when B→A already exists throws/returns error
  - [ ]* 25.6 Test frequency counting — tag appearing in 5 files has GlobalFrequency=5
  - [ ]* 25.7 Test DeleteTagAsync with removeFromFiles=true removes tag from all tag files
  - _Requirements: 5.3, 5.4, 5.5, 5.6_

- [ ] 26. Implement ClipboardService
  - [ ] 26.1 Create `Services/ClipboardService.cs` implementing IClipboardService
  - [ ] 26.2 Implement `CopyTagsAsync` — serialize tag list to comma-separated string, set to system clipboard
  - [ ] 26.3 Implement `PasteTagsAsync` — read clipboard text, parse as comma-separated tags, return list
  - _Requirements: 2.19, 3.12, 9.5_

- [ ] 27. Implement AiTaggerService (stub)
  - [ ] 27.1 Create `Services/AiTaggerService.cs` implementing IAiTaggerService
  - [ ] 27.2 Implement `GetAvailableModelsAsync` — read `ai_models.json` config file, deserialize to List<AiModelInfo>, handle missing/malformed file gracefully
  - [ ] 27.3 Implement `GenerateTagsAsync` — stub returning placeholder tags (real AI integration is external). Set IsProcessing=true during execution, fire TagGenerationCompleted event on completion.
  - [ ] 27.4 Implement `IsProcessing` — track per-image processing state via ConcurrentDictionary
  - _Requirements: 7.1, 7.2, 7.3, 7.4_

- [ ] 28. Implement batch tag operation helpers
  - [ ] 28.1 Create `Services/BatchTagOperationService.cs` (or static helper)
  - [ ] 28.2 Implement batch add — for each target image: read tags, skip if tag already present, append tag, write back. Resolve alias before adding.
  - [ ] 28.3 Implement batch remove — for each target image: read tags, remove matching tag, write back. Preserve all other tags.
  - [ ] 28.4 Publish TagsChangedMessage for each modified image after batch completes
  - _Requirements: 2.11, 2.12_

- [ ]* 29. Write batch operation tests
  - [ ]* 29.1 Test batch add skips duplicates — adding "cat" when "cat" already exists doesn't create duplicate
  - [ ]* 29.2 Test batch add with alias resolution — adding alias "kitty" resolves to "cat" before adding
  - [ ]* 29.3 Test batch remove eliminates only target tag, all other tags preserved
  - [ ]* 29.4 Test batch remove on tag that doesn't exist is a no-op (no error)
  - _Requirements: 2.11, 2.12_

- [ ] 30. Register all services in DI container
  - [ ] 30.1 Update `App.axaml.cs` — register all implemented services as singletons/transients in the IServiceProvider, register IMessenger as WeakReferenceMessenger.Default
  - _Requirements: 12.1, 13.2_

- [ ] 31. Core services checkpoint
  - Run all NUnit tests, verify they pass. Verify DI container resolves all services. Ask user if questions arise.


## Phase 3 — Screens (Parallel Sub-Agents)

> Each screen below is independent. Once Phase 2 is complete, these can be executed in parallel by separate sub-agents. Each task includes both ViewModel and View for a complete screen.

- [ ] 32. Implement Projects Hub screen
  - [ ] 32.1 Create `ViewModels/ProjectsHubViewModel.cs` — inject IProjectService, IFileSystemService, INavigationService, IMessenger
  - [ ] 32.2 Implement observable properties: Projects (ObservableCollection), HasProjects (bool), MasterRootPath (string), IsScanning (bool)
  - [ ] 32.3 Implement `LoadProjectsCommand` — call IProjectService.LoadProjectsAsync, populate Projects collection with card data (name, path, image count, tagged percentage)
  - [ ] 32.4 Implement `ScanMasterRootCommand` — call IFileSystemService.DiscoverProjectFoldersAsync, auto-create Project entries for discovered subfolders
  - [ ] 32.5 Implement `NewProjectCommand` — create new Project via IProjectService, signal MainWindowVM to open ProjectConfig modal
  - [ ] 32.6 Implement `OpenProjectCommand` — navigate to LibraryGrid with selected project, publish ProjectOpenedMessage
  - [ ] 32.7 Create `Views/ProjectsHubView.axaml` — 64px top bar with MasterRootDirectoryPicker (TextBox + Browse button) and NewProject button
  - [ ] 32.8 Implement ProjectCardGrid — ItemsControl with WrapPanel, each card showing name, path, image count, progress bar, hover border (1px solid Primary)
  - [ ] 32.9 Implement empty state placeholder — dashed border, centered text "No datasets found. Create your first project or point to a master folder.", bound to HasProjects
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 1.7_

- [ ] 33. Implement Project Configuration modal
  - [ ] 33.1 Create `ViewModels/ProjectConfigurationViewModel.cs` — inject IProjectService, IAiTaggerService, IFileSystemService, IMessenger
  - [ ] 33.2 Implement observable properties: ProjectName, RootFolderPath, SelectedAiModel, PrefixTagsText, Stages (ObservableCollection<WorkflowStage>), PrefixTagsError (string), HasPrefixTagsError (bool)
  - [ ] 33.3 Implement `BrowseRootFolderCommand` — open folder picker dialog, set RootFolderPath
  - [ ] 33.4 Implement `LoadAiModelsCommand` — call IAiTaggerService.GetAvailableModelsAsync, populate dropdown
  - [ ] 33.5 Implement prefix tags validation — on PrefixTagsText change, validate for invalid characters, set error state
  - [ ] 33.6 Implement workflow stages builder commands: AddStageCommand, RemoveStageCommand, reorder via drag
  - [ ] 33.7 Implement `SaveCommand` — validate all fields, save via IProjectService, create stage subfolders via IFileSystemService.EnsureFolderExistsAsync, publish ProjectConfigSavedMessage, signal close
  - [ ] 33.8 Create `Views/ProjectConfigurationView.axaml` — centered 600px modal overlay with semi-transparent background wash
  - [ ] 33.9 Implement modal form layout — Root folder TextBox + Browse button, AI model ComboBox, Prefix tags TextArea with conditional error border and message, draggable/reorderable stage list with inline editing + delete + "Add Stage" button, Save button
  - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.7, 8.5_

- [ ] 34. Implement Library Grid screen
  - [ ] 34.1 Create `ViewModels/LibraryGridViewModel.cs` — inject IFileSystemService, ITagFileService, ITagDictionaryService, IThumbnailCacheService, IClipboardService, INavigationService, IMessenger
  - [ ] 34.2 Implement observable properties: Stages (ObservableCollection), ActiveStage (WorkflowStage), Images (ObservableCollection<ImageEntry>), SelectedImages (ObservableCollection), FocusedImageIndex (int), FilterText (string), ZoomValue (int, default 160), IsBatchAddOpen (bool), IsBatchRemoveOpen (bool), ProjectName (string), AiModels (ObservableCollection), SelectedAiModel (AiModelInfo)
  - [ ] 34.3 Implement `LoadStagesCommand` — parse workflow stages from disk using WorkflowStageParser, populate sidebar
  - [ ] 34.4 Implement `SelectStageCommand` — load images for selected folder via IFileSystemService.GetImageFilesAsync, build ImageEntry list with tag status
  - [ ] 34.5 Implement `NavigateGridCommand` — arrow key spatial navigation, update FocusedImageIndex, expose IsFocused per ImageEntry
  - [ ] 34.6 Implement `ToggleSelectionCommand` — `x` key toggles IsSelected on focused image, publish ImageSelectionChangedMessage
  - [ ] 34.7 Implement `OpenBatchAddCommand` / `CloseBatchAddCommand` — `+` opens BatchAddPopup, Enter commits tag via BatchTagOperationService, close popup
  - [ ] 34.8 Implement `OpenBatchRemoveCommand` / `CloseBatchRemoveCommand` — `-` opens BatchRemovePopup with tag frequencies, Enter removes tag, close popup
  - [ ] 34.9 Implement `MoveImageCommand` — `[`/`]` move selected images to prev/next stage via IFileSystemService.MoveFileAsync (image + tag file), publish ImageMovedMessage
  - [ ] 34.10 Implement `NavigateStageCommand` — `Alt+[`/`Alt+]` switch active folder view without moving images
  - [ ] 34.11 Implement `DeleteImageCommand` — Delete key recycles selected images + tag files via IFileSystemService.RecycleFileAsync, publish ImageDeletedMessage, auto-advance focus
  - [ ] 34.12 Implement `FocusFilterCommand` — `/` key focuses QuickFilterBar
  - [ ] 34.13 Implement `CopyTagsCommand` / `PasteTagsCommand` — Ctrl+Shift+C/V via IClipboardService
  - [ ] 34.14 Implement `OpenInspectorCommand` — double-click navigates to InspectorMode with selected image
  - [ ] 34.15 Implement quick filter logic — filter Images collection by tag content matching FilterText
  - [ ] 34.16 Implement drag-and-drop — drag thumbnail to sidebar folder moves image, flash target folder in Accent green, set drag opacity 50%
  - [ ] 34.17 Subscribe to messenger events — ImageMovedMessage (refresh folder counts), ImageDeletedMessage (remove from grid), TagsChangedMessage (update status dots), AiTaggingCompletedMessage (update status to Yellow)
  - [ ] 34.18 Create `Views/LibraryGridView.axaml` — three-column layout: 240px left sidebar, fluid center, 64px top bar
  - [ ] 34.19 Implement top bar — ProjectName TextBlock (18px IBM Plex Sans 600), AI model ComboBox, QuickFilterBar TextBox (IBM Plex Mono)
  - [ ] 34.20 Implement left sidebar — WorkflowStageList shared control bound to Stages/ActiveStage
  - [ ] 34.21 Implement center grid — ItemsControl with WrapPanel (min cell size bound to ZoomValue), each item: 1:1 square crop thumbnail, StatusDot bottom-right, hover checkbox top-left, ActiveFocusFrame on focused item
  - [ ] 34.22 Implement ZoomSlider — Slider bottom-right, range 100-400, bound to ZoomValue
  - [ ] 34.23 Implement BatchAddPopup and BatchRemovePopup overlays using shared BatchPopup control
  - [ ] 34.24 Implement empty folder placeholder — centered text "Folder is empty. Drag images here to stage."
  - [ ] 34.25 Implement AI processing indicator — spinning icon overlay with reduced opacity on processing thumbnails
  - [ ] 34.26 Wire HintBar and StatusBar at bottom
  - [ ] 34.27 Implement Escape key handling — dismiss popup → unfocus TextBox → no-op (in priority order)
  - _Requirements: 2.1–2.24, 9.1, 9.4, 9.5_

- [ ] 35. Implement Inspector Mode screen
  - [ ] 35.1 Create `ViewModels/InspectorModeViewModel.cs` — inject ITagFileService, ITagDictionaryService, IFileSystemService, IClipboardService, INavigationService, IMessenger
  - [ ] 35.2 Implement observable properties: CurrentImage (ImageEntry), CurrentImageSource (Bitmap), PrefixTags (IReadOnlyList<string>), AppliedTags (ObservableCollection<string>), TagInputText (string), AutoSuggestTags (ObservableCollection<string>), ImageList (list of images in current folder), CurrentIndex (int)
  - [ ] 35.3 Implement `LoadImageCommand` — load image into CurrentImageSource, load tags from tag file, populate AppliedTags (excluding prefix), set status
  - [ ] 35.4 Implement `CommitTagCommand` — Enter key: validate non-empty, resolve alias via ITagDictionaryService, add to AppliedTags, write via ITagFileService, publish TagsChangedMessage, auto-advance to next untagged (Red/Yellow)
  - [ ] 35.5 Implement `RemoveTagCommand` — tag pill `x` click: remove from AppliedTags, write via ITagFileService, publish TagsChangedMessage
  - [ ] 35.6 Implement `NavigateImageCommand` — Left/Right arrow keys: update CurrentIndex, load prev/next image
  - [ ] 35.7 Implement `MoveImageCommand` — `[`/`]` move current image to prev/next stage, publish ImageMovedMessage, auto-advance
  - [ ] 35.8 Implement `DeleteImageCommand` — Delete key: recycle current image + tag file, publish ImageDeletedMessage, auto-advance
  - [ ] 35.9 Implement `CopyTagsCommand` / `PasteTagsCommand` — Ctrl+Shift+C/V via IClipboardService
  - [ ] 35.10 Implement `GoBackCommand` — Escape navigates back to LibraryGrid
  - [ ] 35.11 Implement auto-suggest — on TagInputText change, query ITagDictionaryService.SearchTagsAsync, populate AutoSuggestTags
  - [ ] 35.12 Implement auto-focus — any letter key focuses tag input (handled in View code-behind, delegates to ViewModel)
  - [ ] 35.13 Subscribe to messenger events — AiTaggingCompletedMessage (refresh tags if current image), ImageMovedMessage (refresh if current image moved externally)
  - [ ] 35.14 Create `Views/InspectorModeView.axaml` — three-column layout: 240px left sidebar (WorkflowStageList), fluid center (Viewbox with image + 2px Warning border), 320px right sidebar
  - [ ] 35.15 Implement top bar — back button, image filename TextBlock, StatusDot badge
  - [ ] 35.16 Implement center pane — Viewbox preserving aspect ratio, Prev/Next overlay buttons (left/right edges)
  - [ ] 35.17 Implement right sidebar — PrefixTags block (read-only, IBM Plex Mono, Surface Elevated bg), 32px TagInput TextBox with auto-suggest Popup, AppliedTagsList WrapPanel of TagPill controls, "Commit & Next" button
  - [ ] 35.18 Implement AI processing spinner overlay on tag list area
  - [ ] 35.19 Wire HintBar and StatusBar at bottom
  - [ ] 35.20 Implement letter key auto-focus in code-behind — PreviewKeyDown handler checks if letter key and no TextBox focused, then focuses TagInput
  - _Requirements: 3.1–3.15, 9.1, 9.5_

- [ ] 36. Implement Tag Dictionary screen
  - [ ] 36.1 Create `ViewModels/TagDictionaryViewModel.cs` — inject ITagDictionaryService, IMessenger
  - [ ] 36.2 Implement observable properties: AllEntries (ObservableCollection<TagDictionaryEntry>), FilteredEntries (filtered view), SearchText (string), SelectedCategory (string), SelectedEntry (TagDictionaryEntry), IsEditing (bool)
  - [ ] 36.3 Implement `LoadEntriesCommand` — call ITagDictionaryService.GetAllEntriesAsync, populate AllEntries
  - [ ] 36.4 Implement category filters — "All Tags" (all), "Needs Alias" (entries with empty aliases), "Orphaned Tags" (frequency=0), "Frequent Tags" (frequency > threshold)
  - [ ] 36.5 Implement search filter — filter entries by SearchText substring match on canonical name and aliases
  - [ ] 36.6 Implement `EditEntryCommand` — double-click enters inline edit mode, allow rename and alias configuration
  - [ ] 36.7 Implement `MergeTagCommand` — prompt for target tag, call ITagDictionaryService.MergeTagsAsync, publish TagDictionaryChangedMessage
  - [ ] 36.8 Implement `DeleteTagCommand` — call ITagDictionaryService.DeleteTagAsync with option to remove from files, publish TagDictionaryChangedMessage
  - [ ] 36.9 Implement `NewTagCommand` — add new entry to dictionary
  - [ ] 36.10 Subscribe to TagDictionaryChangedMessage — refresh entries
  - [ ] 36.11 Create `Views/TagDictionaryView.axaml` — two-column layout: 240px left sidebar with category filter ListBox, fluid center with DataGrid
  - [ ] 36.12 Implement top bar — search/filter TextBox + "New Tag" button
  - [ ] 36.13 Implement DataGrid — sortable columns: Tag Name (IBM Plex Mono), Alias, Global Frequency, Actions (Edit/Merge/Delete buttons)
  - [ ] 36.14 Implement inline edit mode on double-click row
  - [ ] 36.15 Wire HintBar and StatusBar at bottom
  - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.7_

- [ ] 37. Screens checkpoint
  - Verify all five screens render and navigate correctly. Verify data binding works end-to-end. Ask user if questions arise.


## Phase 4 — Integration Wiring (Single Agent, Sequential)

- [ ] 38. Wire keyboard routing on MainWindow
  - [ ] 38.1 Implement global KeyDown handler on MainWindow — intercept Ctrl+Shift+C, Ctrl+Shift+V, `/` and delegate to active ViewModel
  - [ ] 38.2 Implement key propagation — non-global keys forwarded to active child View's KeyDown handler
  - [ ] 38.3 Implement TextBox focus detection — when TextBox has focus, letter keys consumed by TextBox; Escape returns focus to parent container
  - [ ] 38.4 Implement Escape priority chain — dismiss open popup → unfocus TextBox → navigate back (InspectorMode only)
  - [ ] 38.5 Implement HintBar reactive updates — bind HintBar content to current screen + IsTextInputFocused state, update shortcut hints contextually
  - _Requirements: 9.1, 9.2, 9.4, 9.5, 2.8, 2.24, 3.6, 3.15_

- [ ] 39. Wire state persistence into ViewModel lifecycle
  - [ ] 39.1 On app startup — load AppState via IStatePersistenceService, restore window geometry (size + position), navigate to last opened project if set
  - [ ] 39.2 On project open — load ProjectState, restore active stage selection, zoom slider value, selected AI model
  - [ ] 39.3 On state change — debounced save: hook into property change events on relevant ViewModels, trigger IStatePersistenceService save after 500ms debounce
  - [ ] 39.4 On app shutdown — flush any pending state saves
  - [ ] 39.5 Verify every configurable property is persisted — zoom, active stage, window size/position, last project, master root path, selected AI model, last inspected image
  - _Requirements: 11.1, 11.2, 11.3, 11.4_

- [ ] 40. Wire AI tagger background processing
  - [ ] 40.1 On folder load — scan for images without .txt files, queue them for AI tagging via IAiTaggerService
  - [ ] 40.2 Wire processing indicator — set ImageEntry.IsAiProcessing=true while processing, bind to spinner overlay in LibraryGrid thumbnails and InspectorMode tag area
  - [ ] 40.3 On AiTaggingCompletedMessage — create tag file via ITagFileService, set status to Yellow (AutoTagged), refresh UI
  - [ ] 40.4 Wire AI model selection — sync dropdown in LibraryGrid top bar and ProjectConfig modal to IAiTaggerService active model
  - _Requirements: 7.1, 7.2, 7.3, 7.4, 2.21, 3.13_

- [ ] 41. Wire FileSystemWatcher for live updates
  - [ ] 41.1 On project open — start FileSystemWatcher on project root via IFileSystemService.WatchFolder
  - [ ] 41.2 On folder structure change (subfolder add/remove/rename) — refresh WorkflowStage list in sidebar
  - [ ] 41.3 On file change in active folder (image add/remove) — refresh image list in LibraryGrid
  - [ ] 41.4 On source image modification — invalidate thumbnail cache via IThumbnailCacheService.InvalidateAsync
  - [ ] 41.5 On project close/switch — dispose previous FileSystemWatcher
  - _Requirements: 15.3, 15.5_

- [ ] 42. Final integration checkpoint
  - Run all NUnit tests. Verify full navigation flow: ProjectsHub → LibraryGrid → InspectorMode → back. Verify keyboard shortcuts work across all screens. Verify state persistence round-trip. Ask user if questions arise.

## Notes

- Tasks marked with `*` are optional test tasks — recommended but can be skipped for faster MVP
- Phase 1 + 2 MUST be executed by a single agent sequentially to avoid conflicts on shared infrastructure
- Phase 3 screens CAN be executed in parallel by separate sub-agents — each screen is self-contained
- Phase 4 MUST be executed by a single agent — cross-cutting wiring touches multiple screens
- Each task references specific requirements for traceability
- Checkpoints (tasks 11, 31, 37, 42) are pause points for validation
- AI tagger service is stubbed; real model integration is external to this plan
