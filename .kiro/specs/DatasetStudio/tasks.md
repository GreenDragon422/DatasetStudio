# Implementation Plan: DatasetStudio

## Overview

Build a keyboard-first Avalonia/XAML (C#, .NET 10) desktop application for curating and tagging image datasets. The repository currently contains authoritative `.kiro` planning/spec documents, non-authoritative design-reference artifacts, and no application code yet, so implementation must begin with `.kiro` alignment and app bootstrap work before feature delivery. Implementation proceeds bottom-up in five phases:

- **Phase 0 — Preflight Alignment (Single Agent):** Lock `.kiro/specs/DatasetStudio/` as the authoritative design/spec source, settle Avalonia package/setup decisions, and explicitly mark older mockups as non-authoritative where they conflict.
- **Phase 1 — Foundation (Single Agent):** Solution scaffold, design tokens, models, interfaces, DI, navigation, MainWindow shell. Must complete before anything else.
- **Phase 2 — Core Services (Single Agent):** All service implementations with TDD tests. Builds on Phase 1 infra. Must complete before screens.
- **Phase 3 — Screens (Parallel Sub-Agents):** Each screen (ViewModel + View) is independent and can be built in parallel once Phase 2 is done.
- **Phase 4 — Integration Wiring (Single Agent):** Cross-cutting concerns — keyboard routing, state persistence hookup, AI tagger wiring, FileSystemWatcher.

## Phase 0 — Preflight Alignment (Single Agent, Sequential)

- [x] 0. Lock the implementation sources of truth
  - [x] 0.1 Treat the `.kiro/specs/DatasetStudio/` documents as the only authoritative product, architecture, behavior, and implementation-plan source
  - [x] 0.2 Treat all files under `design reference/` as example material only for rough layout inspiration, never as requirements or token authority
  - [x] 0.3 Resolve any conflict in favor of the `.kiro/specs/DatasetStudio/` documents without exception
  - [x] 0.4 Explicitly exclude the old bottom "command terminal" from MVP scope unless it is added back into the requirements, because the current specs define a HintBar and StatusBar instead
  - _Requirements: 9.2, 9.3, 10.1, 10.2, 10.4_

- [x] 0. Confirm Avalonia bootstrap decisions before feature work
  - [x] 0.5 Select the exact Avalonia package version and keep all Avalonia package references on the same version
  - [x] 0.6 Enable compiled bindings by default in the app project and use `x:DataType` across views as they are introduced
  - [x] 0.7 Include `Avalonia.Controls.DataGrid` and its Fluent theme wiring up front so Tag Dictionary work does not require later startup refactors
  - [x] 0.8 Decide the IBM Plex font loading strategy (bundled assets vs. documented install prerequisite) before writing styles
  - _Requirements: 5.1, 10.2, 10.4, 13.1_

## Phase 1 — Foundation (Single Agent, Sequential)

- [x] 1. Create solution and project files
  - [x] 1.1 Create `DatasetStudio.sln` with two projects: `DatasetStudio` (Avalonia app, net10.0) and `DatasetStudio.Tests` (NUnit class library, net10.0)
  - [x] 1.2 Add NuGet references to `DatasetStudio.csproj`: Avalonia, Avalonia.Desktop, Avalonia.Themes.Fluent, Avalonia.Controls.DataGrid, CommunityToolkit.Mvvm, Microsoft.Extensions.DependencyInjection. Enable `<AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>` in the project file.
  - [x] 1.3 Add NuGet references to `DatasetStudio.Tests.csproj`: NUnit, NUnit3TestAdapter, Microsoft.NET.Test.Sdk, project reference to DatasetStudio
  - [x] 1.4 Create folder structure: `Models/`, `ViewModels/`, `Views/`, `Services/`, `Messages/`, `Controls/`, `Resources/`, `Assets/Fonts/`, `Converters/`
  - _Requirements: 13.1, 13.3_

- [x] 2. Create XAML design system resources
  - [x] 2.1 Create `Resources/Colors.axaml` — Gruvbox Light palette with calmer desktop primary accent: Background `#FBF1C7`, Surface `#EBDBB2`, Surface Elevated `#D5C4A1`, Primary `#B57614`, Text `#3C3836`, Muted `#7C6F64`, Accent `#98971A`, Warning `#D79921`, Error `#CC241D`
  - [x] 2.2 Create `Resources/Typography.axaml` — IBM Plex Sans (headings 600/18-24px, body 400/13px, buttons 500/12px uppercase 0.5px tracking), IBM Plex Mono (tags/metadata 500/12px), and wire font assets if fonts are bundled with the app
  - [x] 2.3 Create `Resources/Styles.axaml` — spacing tokens (4px, 8px, 16px, 24px), 2px border radius, 1px solid borders, ActiveFocusFrame style (2px solid Warning `#D79921`)
  - [x] 2.4 Merge all ResourceDictionaries into `App.axaml` and include the DataGrid Fluent theme resource
  - _Requirements: 10.1, 10.2, 10.3, 10.4, 10.5_

- [x] 3. Create core data models
  - [x] 3.1 Create `Models/Project.cs` — Id (string GUID), Name, RootFolderPath, Stages (List<WorkflowStage>), PrefixTags (List<string>), AiModelName, LastModified, TagDictionaryEntries (List<TagDictionaryEntry>), State (ProjectState)
  - [x] 3.2 Create `Models/WorkflowStage.cs` — Order (int), FolderName (string), DisplayName (string)
  - [x] 3.3 Create `Models/ImageEntry.cs` — FilePath, FileName, TagFilePath, Status (TagStatus), Tags (List<string>), IsSelected, IsAiProcessing
  - [x] 3.4 Create `Models/TagStatus.cs` enum — Untagged, AutoTagged, Ready
  - [x] 3.5 Create `Models/TagDictionaryEntry.cs` — CanonicalName, Aliases (List<string>), GlobalFrequency (int)
  - [x] 3.6 Create `Models/AiModelInfo.cs` — Id, DisplayName, ModelPath
  - [x] 3.7 Create `Models/AppState.cs` — LastOpenedProjectId, WindowWidth, WindowHeight, WindowX, WindowY, LastMasterRootDirectory
  - [x] 3.8 Create `Models/ProjectState.cs` — ActiveStageFolderName, ZoomSliderValue, SelectedAiModelName, LastInspectedImagePath
  - _Requirements: 8.2, 6.1, 7.4, 11.1, 11.4_

- [x] 4. Create IMessenger event messages
  - [x] 4.1 Create `Messages/ImageMovedMessage.cs` — record(string ImagePath, string SourceFolder, string TargetFolder)
  - [x] 4.2 Create `Messages/ImageDeletedMessage.cs` — record(string ImagePath, string FolderPath)
  - [x] 4.3 Create `Messages/ImageSelectionChangedMessage.cs` — record(string ImagePath, bool IsSelected)
  - [x] 4.4 Create `Messages/TagsChangedMessage.cs` — record(string ImagePath, IReadOnlyList<string> NewTags)
  - [x] 4.5 Create `Messages/TagDictionaryChangedMessage.cs` — record(string ProjectId)
  - [x] 4.6 Create `Messages/WorkflowStageChangedMessage.cs` — record(string ProjectId, string FolderPath)
  - [x] 4.7 Create `Messages/ProjectOpenedMessage.cs` — record(string ProjectId)
  - [x] 4.8 Create `Messages/AiTaggingCompletedMessage.cs` — record(string ImagePath, IReadOnlyList<string> GeneratedTags)
  - [x] 4.9 Create `Messages/ProjectConfigSavedMessage.cs` — record(string ProjectId)
  - _Requirements: 12.1, 12.2, 12.3, 12.4_

- [x] 5. Create service interfaces
  - [x] 5.1 Create `Services/IFileSystemService.cs` — DiscoverProjectFoldersAsync, GetImageFilesAsync, MoveFileAsync, RecycleFileAsync, EnsureFolderExistsAsync, WatchFolder
  - [x] 5.2 Create `Services/IThumbnailCacheService.cs` — GetThumbnailAsync, InvalidateAsync, InvalidateFolderAsync
  - [x] 5.3 Create `Services/ITagFileService.cs` — ReadTagsAsync, WriteTagsAsync, ReadTagsWithPrefixAsync, GetTagFilePath, TagFileExists
  - [x] 5.4 Create `Services/IAiTaggerService.cs` — GenerateTagsAsync, GetAvailableModelsAsync, IsProcessing, TagGenerationCompleted event
  - [x] 5.5 Create `Services/IProjectService.cs` — LoadProjectsAsync, CreateProjectAsync, SaveProjectAsync, DeleteProjectAsync
  - [x] 5.6 Create `Services/ITagDictionaryService.cs` — GetAllEntriesAsync, SearchTagsAsync, RenameTagAsync, MergeTagsAsync, DeleteTagAsync, AddAliasAsync, ResolveAlias
  - [x] 5.7 Create `Services/INavigationService.cs` — NavigateTo<T>(), NavigateTo<T>(object), GoBack()
  - [x] 5.8 Create `Services/IClipboardService.cs` — CopyTagsAsync, PasteTagsAsync
  - [x] 5.9 Create `Services/IStatePersistenceService.cs` — SaveAppStateAsync, LoadAppStateAsync, SaveProjectStateAsync, LoadProjectStateAsync
  - _Requirements: 13.3, 12.1_

- [x] 6. Create ViewModelBase and MainWindowViewModel
  - [x] 6.1 Create `ViewModels/ViewModelBase.cs` — abstract class extending ObservableRecipient, with `[ObservableProperty]` for HintText (string) and StatusText (string)
  - [x] 6.2 Create `ViewModels/MainWindowViewModel.cs` — `[ObservableProperty]` for CurrentView (ViewModelBase), IsConfigOpen (bool), HintText, StatusText. Inject INavigationService and IMessenger.
  - _Requirements: 13.1, 13.2, 9.2, 9.3_

- [x] 7. Implement NavigationService
  - [x] 7.1 Create `Services/NavigationService.cs` — implements INavigationService, resolves ViewModels from DI, sets MainWindowViewModel.CurrentView, maintains back stack for GoBack()
  - _Requirements: 1.7, 2.18, 3.15_

- [ ] 8. Create DI container and App startup
  - [ ] 8.1 Create `App.axaml.cs` — build IServiceProvider, register all service interfaces → implementations, register all ViewModels, configure ViewModel-to-View resolution for the navigation host, resolve MainWindowViewModel on startup
  - [x] 8.2 Create `Program.cs` entry point with Avalonia AppBuilder configuration
  - _Requirements: 12.1, 13.2_

- [x] 9. Create MainWindow shell
  - [x] 9.1 Create `Views/MainWindow.axaml` — 64px TopBar with AppLogo + Title ("DatasetStudio") and ContentPresenter for screen-specific controls, ContentControl bound to CurrentView, overlay Panel for ProjectConfig modal (toggled by IsConfigOpen), bottom HintBar (24px) and StatusBar (24px)
  - [x] 9.2 Create `Views/MainWindow.axaml.cs` code-behind — DataContext assignment, global KeyDown handler stub (delegates to active ViewModel)
  - _Requirements: 9.2, 9.3, 13.4_

- [x] 10. Create shared UI controls
  - [x] 10.1 Create `Controls/WorkflowStageList.axaml` — reusable ListBox showing workflow folders with stripped numeric prefixes and image counts, bindable ItemsSource and SelectedItem
  - [x] 10.2 Create `Controls/HintBar.axaml` — 24px-height bar, IBM Plex Mono, content bound to HintText property
  - [x] 10.3 Create `Controls/StatusBar.axaml` — 24px-height display-only bar, bound to StatusText property
  - [x] 10.4 Create `Controls/TagPill.axaml` — Border with tag text (IBM Plex Mono) + `x` remove button, Background `#EBDBB2`, border `1px solid #D5C4A1`, exposes Tag (string) and RemoveCommand
  - [x] 10.5 Create `Controls/StatusDot.axaml` — 12px circle, color bound to TagStatus enum (Red=#CC241D, Yellow=#D79921, Green=#98971A)
  - [x] 10.6 Create `Controls/BatchPopup.axaml` — Popup with TextBox + ListBox for autocomplete tag selection, parameterized for add vs. remove mode via Mode property
  - _Requirements: 2.5, 2.23, 3.5, 9.2, 9.3, 10.3, 10.5_

- [ ] 11. Foundation checkpoint
  - Verify solution builds. Verify all interfaces, models, messages, controls, and MainWindow shell compile. Verify design resources load. Ask user if questions arise.


## Phase 2 — Core Services with TDD Tests (Single Agent, Sequential)

- [x] 12. Implement TagFileService
  - [x] 12.1 Create `Services/TagFileService.cs` implementing ITagFileService
  - [x] 12.2 Implement `GetTagFilePath` — derive .txt path from image path (replace extension with .txt)
  - [x] 12.3 Implement `TagFileExists` — check if companion .txt file exists on disk
  - [x] 12.4 Implement `ReadTagsAsync` — read .txt file, split by comma, trim whitespace per tag, return list. Return empty list if file missing or empty.
  - [x] 12.5 Implement `WriteTagsAsync` — join tags with ", " separator, write single line to .txt file
  - [x] 12.6 Implement `ReadTagsWithPrefixAsync` — call ReadTagsAsync, prepend prefix tags to result
  - _Requirements: 6.1, 6.2, 6.3, 6.4_

- [x]* 13. Write TagFileService tests
  - [x]* 13.1 Test `GetTagFilePath` — .png → .txt, .jpg → .txt, .jpeg → .txt, .webp → .txt, .bmp → .txt
  - [x]* 13.2 Test read/write round-trip — write tags then read back produces identical list
  - [x]* 13.3 Test comma parsing with whitespace trimming — "tag1 , tag2 ,  tag3 " → ["tag1", "tag2", "tag3"]
  - [x]* 13.4 Test prefix tag prepending — prefix ["a", "b"] + tags ["c", "d"] → file contains "a, b, c, d"
  - [x]* 13.5 Test empty file returns empty list
  - [x]* 13.6 Test missing file returns empty list (no exception)
  - [x]* 13.7 Test whitespace-only tags are excluded
  - _Requirements: 6.4_

- [x] 14. Implement WorkflowStage parsing logic
  - [x] 14.1 Create `Services/WorkflowStageParser.cs` (static helper or service) — parse numeric prefix from folder name (regex `^\d+[_-]`), extract order int and display name
  - [x] 14.2 Implement sorting: folders with numeric prefixes sorted by prefix value, folders without prefixes sorted alphabetically after numbered ones
  - [x] 14.3 Implement display name stripping: remove numeric prefix and separator (e.g., "01_Inbox" → "Inbox", "02-Review" → "Review")
  - _Requirements: 8.1, 8.2, 8.4_

- [x]* 15. Write WorkflowStage parsing tests
  - [x]* 15.1 Test ordering: ["03_Ready", "01_Inbox", "02_Review"] → sorted as [Inbox(1), Review(2), Ready(3)]
  - [x]* 15.2 Test display name stripping: "01_Inbox" → "Inbox", "02-Review" → "Review"
  - [x]* 15.3 Test folders without numeric prefix sorted after numbered ones
  - [x]* 15.4 Test single-digit and multi-digit prefixes: "1_A", "10_B", "2_C" → [A(1), C(2), B(10)]
  - _Requirements: 8.2_

- [x] 16. Implement FileSystemService
  - [x] 16.1 Create `Services/FileSystemService.cs` implementing IFileSystemService
  - [x] 16.2 Implement `GetImageFilesAsync` — enumerate files in folder, filter by extensions (.png, .jpg, .jpeg, .webp, .bmp), return sorted list
  - [x] 16.3 Implement `DiscoverProjectFoldersAsync` — scan master root for subfolders containing `.datasetstudio.json`
  - [x] 16.4 Implement `MoveFileAsync` — File.Move with overwrite protection
  - [x] 16.5 Implement `RecycleFileAsync` — send to OS recycle bin (use Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile with RecycleOption on Windows)
  - [x] 16.6 Implement `EnsureFolderExistsAsync` — Directory.CreateDirectory if not exists
  - [x] 16.7 Implement `WatchFolder` — return configured FileSystemWatcher for project root
  - _Requirements: 8.1, 8.3, 8.5, 14.1, 14.3_

- [x]* 17. Write FileSystemService tests
  - [x]* 17.1 Test `GetImageFilesAsync` returns only supported extensions, ignores .txt and other files
  - [x]* 17.2 Test `MoveFileAsync` — source gone, target exists
  - [x]* 17.3 Test `EnsureFolderExistsAsync` — creates folder, no error if already exists
  - [x]* 17.4 Test `DiscoverProjectFoldersAsync` — finds folders with .datasetstudio.json, ignores others
  - _Requirements: 8.3, 14.1_

- [x] 18. Implement ProjectService
  - [x] 18.1 Create `Services/ProjectService.cs` implementing IProjectService
  - [x] 18.2 Implement `LoadProjectsAsync` — scan for `.datasetstudio.json` files in known paths (currently the persisted `LastMasterRootDirectory`), deserialize with System.Text.Json
  - [x] 18.3 Implement `CreateProjectAsync` — generate GUID, build default Project with auto-detected stages, write `.datasetstudio.json`
  - [x] 18.4 Implement `SaveProjectAsync` — serialize Project to `.datasetstudio.json` with indented formatting, including the nested `state` block
  - [x] 18.5 Implement `DeleteProjectAsync` — remove `.datasetstudio.json` file
  - [x] 18.6 Handle malformed JSON — catch JsonException, return default Project using folder name as project name
  - _Requirements: 1.3, 4.7, 11.4_

- [x]* 19. Write ProjectService tests
  - [x]* 19.1 Test save/load round-trip — all fields preserved (id, name, stages, prefixTags, aiModelName, state block)
  - [x]* 19.2 Test malformed JSON falls back to default Project
  - [x]* 19.3 Test CreateProjectAsync generates valid GUID and writes file
  - _Requirements: 11.4_

- [x] 20. Implement StatePersistenceService
  - [x] 20.1 Create `Services/StatePersistenceService.cs` implementing IStatePersistenceService
  - [x] 20.2 Implement `SaveAppStateAsync` / `LoadAppStateAsync` — persist AppState to `datasetstudio-settings.json` in Environment.SpecialFolder.ApplicationData
  - [x] 20.3 Implement `SaveProjectStateAsync` / `LoadProjectStateAsync` — read/write the `state` block within `.datasetstudio.json`
  - [x] 20.4 Implement debounced save — use a Timer that resets on each save call, fires after 500ms of inactivity
  - [x] 20.5 Handle missing files — return default AppState/ProjectState with sensible defaults
  - _Requirements: 11.1, 11.2, 11.3, 11.4_

- [x]* 21. Write StatePersistenceService tests
  - [x]* 21.1 Test AppState round-trip — window geometry, last project ID, last master root directory all preserved
  - [x]* 21.2 Test ProjectState round-trip — active stage, zoom value, selected AI model, last inspected image path all preserved
  - [x]* 21.3 Test missing settings file returns default AppState
  - [x]* 21.4 Test missing project state returns default ProjectState
  - _Requirements: 11.1, 11.2_

- [x] 22. Implement ThumbnailCacheService
  - [x] 22.1 Create `Services/ThumbnailCacheService.cs` implementing IThumbnailCacheService
  - [x] 22.2 Implement `GetThumbnailAsync` — compute cache path from image path + size, check if cached file exists and source timestamp matches, return cached stream on hit
  - [x] 22.3 Implement cache miss path — load source image, resize to requested size (square crop), encode as WebP, write to `.datasetstudio-cache/` subfolder, return stream
  - [x] 22.4 Implement `InvalidateAsync` — delete cached thumbnail for a single image
  - [x] 22.5 Implement `InvalidateFolderAsync` — delete all cached thumbnails in a folder's cache directory
  - _Requirements: 15.1, 15.2, 15.3, 14.2_

- [x]* 23. Write ThumbnailCacheService tests
  - [x]* 23.1 Test cache miss generates thumbnail file in correct cache path
  - [x]* 23.2 Test cache hit returns existing file without regenerating
  - [x]* 23.3 Test stale cache — when source timestamp changes, old cache is invalidated and new thumbnail generated
  - [x]* 23.4 Test InvalidateAsync removes the cached file
  - _Requirements: 15.1, 15.2, 15.3_

- [x] 24. Implement TagDictionaryService
  - [x] 24.1 Create `Services/TagDictionaryService.cs` implementing ITagDictionaryService
  - [x] 24.2 Implement `GetAllEntriesAsync` — scan all tag files in project, build frequency map, return TagDictionaryEntry list
  - [x] 24.3 Implement `SearchTagsAsync` — filter entries by substring match on canonical name and aliases
  - [x] 24.4 Implement `AddAliasAsync` — add alias mapping to a canonical tag entry
  - [x] 24.5 Implement `ResolveAlias` — given input string, return canonical tag name if alias exists, otherwise return input unchanged
  - [x] 24.6 Implement `RenameTagAsync` — rename tag in dictionary and update all tag files that contain it
  - [x] 24.7 Implement `MergeTagsAsync` — merge source tag into target, update all tag file references, detect circular alias before merge
  - [x] 24.8 Implement `DeleteTagAsync` — remove from dictionary, optionally scan and remove from all tag files
  - [x] 24.9 Implement in-memory cache — load dictionary once per project open, subscribe to TagDictionaryChangedMessage for refresh
  - _Requirements: 5.3, 5.4, 5.5, 5.6, 15.4_

- [x]* 25. Write TagDictionaryService tests
  - [x]* 25.1 Test alias resolution — alias "cat" → canonical "feline" returns "feline"
  - [x]* 25.2 Test unknown alias returns input unchanged
  - [x]* 25.3 Test RenameTagAsync updates tag in all files
  - [x]* 25.4 Test MergeTagsAsync merges source into target across all files
  - [x]* 25.5 Test circular alias detection — merging A→B when B→A already exists throws/returns error
  - [x]* 25.6 Test frequency counting — tag appearing in 5 files has GlobalFrequency=5
  - [x]* 25.7 Test DeleteTagAsync with removeFromFiles=true removes tag from all tag files
  - _Requirements: 5.3, 5.4, 5.5, 5.6_

- [x] 26. Implement ClipboardService
  - [x] 26.1 Create `Services/ClipboardService.cs` implementing IClipboardService
  - [x] 26.2 Implement `CopyTagsAsync` — serialize tag list to comma-separated string, set to system clipboard
  - [x] 26.3 Implement `PasteTagsAsync` — read clipboard text, parse as comma-separated tags, return list
  - _Requirements: 2.19, 3.12, 9.5_

- [x] 27. Implement AiTaggerService (stub)
  - [x] 27.1 Create `Services/AiTaggerService.cs` implementing IAiTaggerService
  - [x] 27.2 Implement `GetAvailableModelsAsync` — read `ai_models.json` config file, deserialize to List<AiModelInfo>, handle missing/malformed file gracefully
  - [x] 27.3 Implement `GenerateTagsAsync` — stub returning placeholder tags (real AI integration is external). Set IsProcessing=true during execution, fire TagGenerationCompleted event on completion.
  - [x] 27.4 Implement `IsProcessing` — track per-image processing state via ConcurrentDictionary
  - _Requirements: 7.1, 7.2, 7.3, 7.4_

- [x] 28. Implement batch tag operation helpers
  - [x] 28.1 Create `Services/BatchTagOperationService.cs` (or static helper)
  - [x] 28.2 Implement batch add — for each target image: read tags, skip if tag already present, append tag, write back. Resolve alias before adding.
  - [x] 28.3 Implement batch remove — for each target image: read tags, remove matching tag, write back. Preserve all other tags.
  - [x] 28.4 Publish TagsChangedMessage for each modified image after batch completes
  - _Requirements: 2.11, 2.12_

- [x]* 29. Write batch operation tests
  - [x]* 29.1 Test batch add skips duplicates — adding "cat" when "cat" already exists doesn't create duplicate
  - [x]* 29.2 Test batch add with alias resolution — adding alias "kitty" resolves to "cat" before adding
  - [x]* 29.3 Test batch remove eliminates only target tag, all other tags preserved
  - [x]* 29.4 Test batch remove on tag that doesn't exist is a no-op (no error)
  - _Requirements: 2.11, 2.12_

- [x] 30. Register all services in DI container
  - [x] 30.1 Update `App.axaml.cs` — register all implemented services as singletons/transients in the IServiceProvider, register IMessenger as WeakReferenceMessenger.Default
  - _Requirements: 12.1, 13.2_

- [x] 31. Core services checkpoint
  - [x] Run all NUnit tests, verify they pass.
  - [x] Verify DI container resolves all services.
  - [x] Ask user if questions arise.


## Phase 3 — Screens (Parallel Sub-Agents)

> Each screen below is independent. Once Phase 2 is complete, these can be executed in parallel by separate sub-agents. Each task includes both ViewModel and View for a complete screen.

- [x] 32. Implement Projects Hub screen
  - [x] 32.1 Create `ViewModels/ProjectsHubViewModel.cs` — inject IProjectService, IFileSystemService, INavigationService, IMessenger
  - [x] 32.2 Implement observable properties: Projects (ObservableCollection), HasProjects (bool), MasterRootPath (string), IsScanning (bool)
  - [x] 32.3 Implement `LoadProjectsCommand` — call IProjectService.LoadProjectsAsync, populate Projects collection with card data (name, path, image count, tagged percentage)
  - [x] 32.4 Implement `ScanMasterRootCommand` — call IFileSystemService.DiscoverProjectFoldersAsync, auto-create Project entries for discovered subfolders
  - [x] 32.5 Implement `NewProjectCommand` — create new Project via IProjectService, signal MainWindowVM to open ProjectConfig modal
  - [x] 32.6 Implement `OpenProjectCommand` — navigate to LibraryGrid with selected project, publish ProjectOpenedMessage
  - [x] 32.7 Create `Views/ProjectsHubView.axaml` — 64px top bar with MasterRootDirectoryPicker (TextBox + Browse button) and NewProject button
  - [x] 32.8 Implement ProjectCardGrid — ItemsControl with WrapPanel, each card showing name, path, image count, progress bar, hover border (1px solid Primary)
  - [x] 32.9 Implement empty state placeholder — dashed border, centered text "No datasets found. Create your first project or point to a master folder.", bound to HasProjects
  - Note (2026-03-27): The top bar now matches Requirement 1.4 more closely: browsing a master root scans automatically, pressing Enter in the path field scans typed paths, and a master-root FileSystemWatcher resyncs project cards when project folders or `.datasetstudio.json` files change.
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 1.7_

- [x] 33. Implement Project Configuration modal
  - [x] 33.1 Create `ViewModels/ProjectConfigurationViewModel.cs` — inject IProjectService, IAiTaggerService, IFileSystemService, IMessenger
  - [x] 33.2 Implement observable properties: ProjectName, RootFolderPath, SelectedAiModel, PrefixTagsText, Stages (ObservableCollection<WorkflowStage>), PrefixTagsError (string), HasPrefixTagsError (bool)
  - [x] 33.3 Implement `BrowseRootFolderCommand` — open folder picker dialog, set RootFolderPath
  - [x] 33.4 Implement `LoadAiModelsCommand` — call IAiTaggerService.GetAvailableModelsAsync, populate dropdown
  - [x] 33.5 Implement prefix tags validation — on PrefixTagsText change, validate for invalid characters, set error state
  - [x] 33.6 Implement workflow stages builder commands: AddStageCommand, RemoveStageCommand, reorder via drag
  - [x] 33.7 Implement `SaveCommand` — validate all fields, save via IProjectService, create stage subfolders via IFileSystemService.EnsureFolderExistsAsync, publish ProjectConfigSavedMessage, signal close
  - [x] 33.8 Create `Views/ProjectConfigurationView.axaml` — centered 600px modal overlay with semi-transparent background wash
  - [x] 33.9 Implement modal form layout — Root folder TextBox + Browse button, AI model ComboBox, Prefix tags TextArea with conditional error border and message, draggable/reorderable stage list with inline editing + delete + "Add Stage" button, Save button
  - Note (2026-03-27): The modal shell and form layout were tightened so the content auto-fits within the overlay width, and stage-row actions now flow onto a second row instead of clipping on narrower popup widths.
  - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.7, 8.5_

- [ ] 34. Implement Library Grid screen
  - [x] 34.1 Create `ViewModels/LibraryGridViewModel.cs` — inject IFileSystemService, ITagFileService, ITagDictionaryService, IThumbnailCacheService, IClipboardService, INavigationService, IMessenger
  - [x] 34.2 Implement observable properties: Stages (ObservableCollection), ActiveStage (WorkflowStage), Images (ObservableCollection<ImageEntry>), SelectedImages (ObservableCollection), FocusedImageIndex (int), FilterText (string), ZoomValue (int, default 160), IsBatchAddOpen (bool), IsBatchRemoveOpen (bool), ProjectName (string), AiModels (ObservableCollection), SelectedAiModel (AiModelInfo)
  - [x] 34.3 Implement `LoadStagesCommand` — parse workflow stages from disk using WorkflowStageParser, populate sidebar
  - [x] 34.4 Implement `SelectStageCommand` — load images for selected folder via IFileSystemService.GetImageFilesAsync, build ImageEntry list with tag status
  - [x] 34.5 Implement `NavigateGridCommand` — arrow key spatial navigation, update FocusedImageIndex, expose IsFocused per ImageEntry
  - [x] 34.6 Implement `ToggleSelectionCommand` — `x` key toggles IsSelected on focused image, publish ImageSelectionChangedMessage
  - [x] 34.7 Implement `OpenBatchAddCommand` / `CloseBatchAddCommand` — `+` opens BatchAddPopup, Enter commits tag via BatchTagOperationService, close popup
  - [x] 34.8 Implement `OpenBatchRemoveCommand` / `CloseBatchRemoveCommand` — `-` opens BatchRemovePopup with tag frequencies, Enter removes tag, close popup
  - [x] 34.9 Implement `MoveImageCommand` — `[`/`]` move selected images to prev/next stage via IFileSystemService.MoveFileAsync (image + tag file), publish ImageMovedMessage
  - [x] 34.10 Implement `NavigateStageCommand` — `Alt+[`/`Alt+]` switch active folder view without moving images
  - [x] 34.11 Implement `DeleteImageCommand` — Delete key recycles selected images + tag files via IFileSystemService.RecycleFileAsync, publish ImageDeletedMessage, auto-advance focus
  - [x] 34.12 Implement `FocusFilterCommand` — `/` key focuses QuickFilterBar
  - [x] 34.13 Implement `CopyTagsCommand` / `PasteTagsCommand` — Ctrl+Shift+C/V via IClipboardService
  - [x] 34.14 Implement `OpenInspectorCommand` — double-click navigates to InspectorMode with selected image
  - [x] 34.15 Implement quick filter logic — filter Images collection by tag content matching FilterText
  - [ ] 34.16 Implement drag-and-drop — drag thumbnail to sidebar folder moves image, flash target folder in Accent green, set drag opacity 50%
  - [x] 34.17 Subscribe to messenger events — ImageMovedMessage (refresh folder counts), ImageDeletedMessage (remove from grid), TagsChangedMessage (update status dots), AiTaggingCompletedMessage (update status to Yellow)
  - [x] 34.18 Create `Views/LibraryGridView.axaml` — three-column layout: 240px left sidebar, fluid center, 64px top bar
  - [x] 34.19 Implement top bar — ProjectName TextBlock (18px IBM Plex Sans 600), AI model ComboBox, QuickFilterBar TextBox (IBM Plex Mono)
  - [x] 34.20 Implement left sidebar — WorkflowStageList shared control bound to Stages/ActiveStage
  - [ ] 34.21 Implement center grid — virtualization-friendly image grid (prefer `ItemsRepeater` or an equivalent virtualizing layout over a fully materialized WrapPanel for large folders), min cell size bound to ZoomValue, each item: 1:1 square crop thumbnail, StatusDot bottom-right, hover checkbox top-left, ActiveFocusFrame on focused item
  - [x] 34.22 Implement ZoomSlider — Slider bottom-right, range 100-400, bound to ZoomValue
  - [x] 34.23 Implement BatchAddPopup and BatchRemovePopup overlays using shared BatchPopup control
  - [x] 34.24 Implement empty folder placeholder — centered text "Folder is empty. Drag images here to stage."
  - [ ] 34.25 Implement AI processing indicator — spinning icon overlay with reduced opacity on processing thumbnails
  - [x] 34.26 Wire HintBar and StatusBar at bottom
  - [x] 34.27 Implement Escape key handling — dismiss popup → unfocus TextBox → no-op (in priority order)
  - Progress note (2026-03-27): Review Workspace now supports keyboard-driven batch add/remove popups anchored from the filter bar, selection-scoped or folder-wide batch tag operations, selected-image move/delete behavior, focused-image clipboard copy/paste, and popup-priority Escape handling. This slice is unit-tested for batch scope, move behavior, and clipboard normalization.
  - _Requirements: 2.1–2.24, 9.1, 9.4, 9.5_

- [x] 35. Implement Inspector Mode screen
  - [x] 35.1 Create `ViewModels/InspectorModeViewModel.cs` — inject ITagFileService, ITagDictionaryService, IFileSystemService, IClipboardService, INavigationService, IMessenger
  - [x] 35.2 Implement observable properties: CurrentImage (ImageEntry), CurrentImageSource (Bitmap), PrefixTags (IReadOnlyList<string>), AppliedTags (ObservableCollection<string>), TagInputText (string), AutoSuggestTags (ObservableCollection<string>), ImageList (list of images in current folder), CurrentIndex (int)
  - [x] 35.3 Implement `LoadImageCommand` — load image into CurrentImageSource, load tags from tag file, populate AppliedTags (excluding prefix), set status
  - [x] 35.4 Implement `CommitTagCommand` — Enter key: validate non-empty, resolve alias via ITagDictionaryService, add to AppliedTags, write via ITagFileService, publish TagsChangedMessage, auto-advance to next untagged (Red/Yellow)
  - [x] 35.5 Implement `RemoveTagCommand` — tag pill `x` click: remove from AppliedTags, write via ITagFileService, publish TagsChangedMessage
  - [x] 35.6 Implement `NavigateImageCommand` — Left/Right arrow keys: update CurrentIndex, load prev/next image
  - [x] 35.7 Implement `MoveImageCommand` — `[`/`]` move current image to prev/next stage, publish ImageMovedMessage, auto-advance
  - [x] 35.8 Implement `DeleteImageCommand` — Delete key: recycle current image + tag file, publish ImageDeletedMessage, auto-advance
  - [x] 35.9 Implement `CopyTagsCommand` / `PasteTagsCommand` — Ctrl+Shift+C/V via IClipboardService
  - [x] 35.10 Implement `GoBackCommand` — Escape navigates back to LibraryGrid
  - [x] 35.11 Implement auto-suggest — on TagInputText change, query ITagDictionaryService.SearchTagsAsync, populate AutoSuggestTags
  - [x] 35.12 Implement auto-focus — any letter key focuses tag input (handled in View code-behind, delegates to ViewModel)
  - [x] 35.13 Subscribe to messenger events — AiTaggingCompletedMessage (refresh tags if current image), ImageMovedMessage (refresh if current image moved externally)
  - [x] 35.14 Create `Views/InspectorModeView.axaml` — three-column layout: 240px left sidebar (WorkflowStageList), fluid center (Viewbox with image + 2px Warning border), 320px right sidebar
  - [x] 35.15 Implement top bar — back button, image filename TextBlock, StatusDot badge
  - [x] 35.16 Implement center pane — Viewbox preserving aspect ratio, Prev/Next overlay buttons (left/right edges)
  - [x] 35.17 Implement right sidebar — PrefixTags block (read-only, IBM Plex Mono, Surface Elevated bg), 32px TagInput TextBox with auto-suggest Popup, AppliedTagsList WrapPanel of TagPill controls, "Commit & Next" button
  - [x] 35.18 Implement AI processing spinner overlay on tag list area
  - [x] 35.19 Wire HintBar and StatusBar at bottom
  - [x] 35.20 Implement letter key auto-focus in code-behind — PreviewKeyDown handler checks if letter key and no TextBox focused, then focuses TagInput
  - Note (2026-03-27): Prev/next navigation is fully keyboard-driven and functional, but it currently switches images immediately instead of animating the transition. If we want literal sliding motion from Requirement 3.9, that is now a polish pass rather than a missing workflow.
  - _Requirements: 3.1–3.15, 9.1, 9.5_

- [x] 36. Implement Tag Dictionary screen
  - [x] 36.1 Create `ViewModels/TagDictionaryViewModel.cs` — inject ITagDictionaryService, IMessenger
  - [x] 36.2 Implement observable properties: AllEntries (ObservableCollection<TagDictionaryEntry>), FilteredEntries (filtered view), SearchText (string), SelectedCategory (string), SelectedEntry (TagDictionaryEntry), IsEditing (bool)
  - [x] 36.3 Implement `LoadEntriesCommand` — call ITagDictionaryService.GetAllEntriesAsync, populate AllEntries
  - [x] 36.4 Implement category filters — "All Tags" (all), "Needs Alias" (entries with empty aliases), "Orphaned Tags" (frequency=0), "Frequent Tags" (frequency > threshold)
  - [x] 36.5 Implement search filter — filter entries by SearchText substring match on canonical name and aliases
  - [x] 36.6 Implement `EditEntryCommand` — double-click enters inline edit mode, allow rename and alias configuration
  - [x] 36.7 Implement `MergeTagCommand` — prompt for target tag, call ITagDictionaryService.MergeTagsAsync, publish TagDictionaryChangedMessage
  - [x] 36.8 Implement `DeleteTagCommand` — call ITagDictionaryService.DeleteTagAsync with option to remove from files, publish TagDictionaryChangedMessage
  - [x] 36.9 Implement `NewTagCommand` — add new entry to dictionary
  - [x] 36.10 Subscribe to TagDictionaryChangedMessage — refresh entries
  - [x] 36.11 Create `Views/TagDictionaryView.axaml` — two-column layout: 240px left sidebar with category filter ListBox, fluid center with DataGrid
  - [x] 36.12 Implement top bar — search/filter TextBox + "New Tag" button
  - [x] 36.13 Implement DataGrid — sortable columns: Tag Name (IBM Plex Mono), Alias, Global Frequency, Actions (Edit/Merge/Delete buttons)
  - [x] 36.14 Implement inline edit mode on double-click row
  - [x] 36.15 Wire HintBar and StatusBar at bottom
  - Note (2026-03-27): Shared DataGrid and control styling were adjusted for the Gruvbox Light palette so tag text and action controls remain readable against the screen background and grid chrome.
  - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.7_

- [ ] 37. Screens checkpoint
  - Verify all five screens render and navigate correctly. Verify data binding works end-to-end. Ask user if questions arise.
  - Progress note (2026-03-27): Shared shell visuals were retuned for readability: buttons now use a neutral default-action treatment instead of the older orange fill, Fluent accent/selection colors were shifted away from blue, low-contrast item text was strengthened, the app icon was refreshed, and updated headless captures were regenerated for review. The checkpoint remains open until the remaining screen work is complete.
  - Progress note (2026-03-27): The shared palette was realigned to the authoritative Gruvbox-light base colors with the user-selected calmer desktop primary `#B57614`, while keeping the olive accent `#98971A`. Derived surface, button, and selection shades were retuned from those anchors and headless screen captures were regenerated for review.
  - Progress note (2026-03-27): Remaining view-local styling in `ProjectsHubView.axaml` and `ProjectConfigurationView.axaml` was moved into the shared theme so screen XAML stays structural. Shared classes like `project-card-frame`, `form-section`, `prefix-tags-frame`, and `stage-row` are now themed centrally in `Resources/Styles.axaml`.
  - Progress note (2026-03-27): Shared theme selectors were strengthened for category-list and form-label text, the shell icon asset was replaced with the accepted abstract block mark, and the Library Grid screen copy now explains the stage-review/batch-tagging workflow more clearly under the user-facing name "Review Workspace".
  - Progress note (2026-03-27): The shared visual contract was tightened again so future screens can reuse generic chrome instead of restyling controls inline. `Resources/Styles.axaml` now owns reusable shells and helper classes like `screen-header-bar`, `plain-list`, `chromeless`, `chromeless-input`, `shell-bar-text`, `thumbnail-placeholder`, `list-row-shell`, `suggestion-row`, and `meta-text`, while current views were stripped back to layout, bindings, and screen-specific structure.
  - Progress note (2026-03-27): Inspector Mode is now implemented and reachable from Review Workspace via double-click or Enter on the focused image. The screen loads the current stage/image from `ProjectState.LastInspectedImagePath`, supports keyboard-first tag commit/remove/copy/paste/move/delete flows, and is covered by unit tests plus headless rendering smoke.
  - Progress note (2026-03-27): Shared chrome was consolidated further so new screens can compose theme variants instead of reintroducing inline spacing and control-state styling. `Resources/Styles.axaml` now owns `screen-root`, card padding variants, wrap-tile/list-item spacing, action-row shells, dialog action rows, edge-nav buttons, ComboBox item hover/selection states, and themed Slider track selectors; current screen XAML was updated to consume those shared classes.
  - Progress note (2026-03-27): Reusable controls were tightened to keep visual behavior in XAML and shared theme selectors instead of code-behind self-`DataContext` patterns. `WorkflowStageList`, `BatchPopup`, `TagPill`, and `StatusDot` now bind through their XAML roots, while `plain-list` row states and the new `mono-caption` text treatment are centralized for reuse across screens.
  - Progress note (2026-03-27): Another theme sweep consolidated recurring screen/header/form/footer structure into shared classes like `screen-header-layout`, `screen-title-stack`, `stage-sidebar-layout`, `form-row`, `info-bar-layout`, `flow-actions`, `inline-glyph`, and `trailing-meta`. Current screens now compose those generic building blocks instead of re-styling the same layout chrome in each view, and the remaining base control leaks were narrowed with shared `ComboBox`, `CheckBox`, `ScrollViewer`, and `ScrollBar` theming.


## Phase 4 — Integration Wiring (Single Agent, Sequential)

- [x] 38. Wire keyboard routing on MainWindow
  - [x] 38.1 Implement global KeyDown handler on MainWindow — intercept Ctrl+Shift+C, Ctrl+Shift+V, `/` and delegate to active ViewModel
  - [x] 38.2 Implement key propagation — non-global keys forwarded to the active rendered child screen via the shared `ScreenViewBase<TViewModel>` contract, with modal content taking priority over the underlying main screen
  - [x] 38.3 Implement TextBox focus detection — the shared screen base now detects editable text focus, keeps ordinary typing with the `TextBox`, and injects the common `Esc` unfocus/leave-field behavior
  - [x] 38.4 Implement Escape priority chain — dismiss open popup → unfocus TextBox → navigate back (InspectorMode only)
  - [x] 38.5 Implement HintBar reactive updates — the shared screen base now formats registered shortcuts into `HintText` and refreshes them as focus/state changes, while `MainWindowViewModel` mirrors the active main or modal screen
  - Progress note (2026-03-27): Keyboard routing is now fully standardized around `ScreenViewBase<TViewModel>` + `ScreenViewModelBase`. `MainWindow` tunnels key input to the active rendered screen so `/` and `Ctrl+Shift+C`/`V` still reach the screen contract even when focus sits inside child controls, the shared base preserves editable text behavior and `Esc` leave-field handling, Library Grid popups win the first `Esc` rung, and Inspector Mode only navigates back after the text field has been unfocused. Headless tests verify slash focus routing, global copy routing, and the Inspector Escape priority chain.
  - _Requirements: 9.1, 9.2, 9.4, 9.5, 2.8, 2.24, 3.6, 3.15_

- [x] 39. Wire state persistence into ViewModel lifecycle
  - [x] 39.1 On app startup — load AppState via IStatePersistenceService, restore window geometry (size + position), navigate to last opened project if set
  - [x] 39.2 On project open — load ProjectState, restore active stage selection, zoom slider value, selected AI model
  - [x] 39.3 On state change — debounced save: hook into property change events on relevant ViewModels, trigger IStatePersistenceService save after 500ms debounce
  - [x] 39.4 On app shutdown — flush any pending state saves
  - [x] 39.5 Verify every configurable property is persisted — zoom, active stage, window size/position, last project, master root path, selected AI model, last inspected image
  - Progress note (2026-03-27): App startup now restores `AppState`, reapplies saved window geometry, and jumps straight back into the last opened project when it still exists. Projects Hub persists the master root and last opened project id, Review Workspace and Inspector Mode reload the latest `ProjectState` on open and save stage/zoom/model/focused-image changes through the debounced persistence service, and shutdown now flushes any queued writes. Coverage was expanded with app-state, project-state, and flush tests, bringing the suite to 65 unit tests plus 4 headless tests.
  - _Requirements: 11.1, 11.2, 11.3, 11.4_

- [x] 40. Wire AI tagger background processing
  - [x] 40.1 On folder load — scan for images without .txt files, queue them for AI tagging via IAiTaggerService
  - [x] 40.2 Wire processing indicator — set ImageEntry.IsAiProcessing=true while processing, bind to spinner overlay in LibraryGrid thumbnails and InspectorMode tag area
  - [x] 40.3 On AiTaggingCompletedMessage — create tag file via ITagFileService, set status to Yellow (AutoTagged), refresh UI
  - [x] 40.4 Wire AI model selection — sync dropdown in LibraryGrid top bar and ProjectConfig modal to IAiTaggerService active model
  - Progress note (2026-03-27): Review Workspace and Inspector Mode now queue missing-tag images in the background using the configured AI model, shared processing overlays render while tagging is active, and a singleton `AiTaggingCoordinator` persists generated `.txt` files before publishing `AiTaggingCompletedMessage` back into the screen layer. Library Grid also now loads the shared AI model registry into its top-bar dropdown. Coverage was expanded with queueing tests plus a coordinator persistence/message test, and the full solution verifies cleanly with `dotnet test DatasetStudio.sln`.
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
- The `.kiro/specs/DatasetStudio/` documents are authoritative for scope, behavior, architecture, and visual tokens
- Files under `design reference/` are examples only and must not override `.kiro` decisions
