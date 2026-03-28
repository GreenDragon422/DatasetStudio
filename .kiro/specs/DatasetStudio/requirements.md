# Requirements Document

## Authority Note

The files under `.kiro/specs/DatasetStudio/` are the authoritative source for DatasetStudio requirements, design, and implementation planning. If anything under `design reference/` conflicts with this document or its sibling `.kiro` documents, the `.kiro` documents win.

## Introduction

DatasetStudio is a high-density, keyboard-first desktop application (Avalonia/XAML, C#, .NET 10) for rapidly curating and tagging image datasets used in AI inference training. The application enables machine learning engineers and dataset curators to process thousands of images without breaking flow state, through workflow staging folders, batch tag manipulation, background AI auto-tagging, and lightning-fast keyboard-driven navigation. The visual identity follows a warm, Gruvbox Light palette with IBM Plex Sans/Mono typography, sharp 2px border radiuses, and an earthy, retro-utilitarian aesthetic.

## Glossary

- **Application**: The DatasetStudio desktop application built with Avalonia/XAML (C#, .NET 10)
- **Project**: A dataset project mapped to a single root folder on disk, containing workflow stage subfolders and image files with associated tag text files
- **Root_Folder**: The top-level directory on disk that a Project maps to, containing all workflow stage subfolders
- **Master_Root_Directory**: A parent directory containing multiple Project root folders, used for auto-discovery of projects
- **Workflow_Stage**: A named subfolder within a Project's Root_Folder representing a step in the curation pipeline (e.g., Inbox, Review, Ready). Stages have a defined sequential order
- **Tag**: A single descriptive keyword or phrase stored in a text file alongside an image, following Booru-style comma-separated format
- **Tag_File**: A plain text file (same base name as its image, with .txt extension) containing comma-separated tags for that image
- **Prefix_Tags**: A fixed set of keyword tags configured per Project that are prepended to all tag files when saved
- **Tag_Dictionary**: A centralized taxonomy of all known tags within a single Project, with alias mappings and frequency tracking
- **Alias**: An alternative name for a tag in the Tag_Dictionary that automatically resolves to the canonical tag name
- **Status_Dot**: A colored indicator on each image thumbnail showing tagging state: Red (untagged), Yellow (auto-tagged/needs review), Green (ready for training)
- **AI_Tagger**: A background process that automatically generates initial tags for untagged images using a configurable AI model (e.g., WD14 ViT v2, DeepDanbooru, CLIP Interrogator)
- **Quick_Filter_Bar**: A text input in the Project Overview top bar for live-filtering displayed images by tag content
- **Status_Bar**: A persistent display-only bar at the bottom of every screen showing feedback messages and contextual status information
- **Hint_Bar**: A persistent bar displaying available keyboard shortcuts for the current screen context
- **Projects_Hub**: The entry-point screen displaying a grid of all dataset projects with progress metrics
- **Project_Overview**: The screen showing a dense image grid with workflow-stage sidebar for mass selection, stage navigation, and batch tag operations
- **Inspector_Mode**: The screen showing a single large image preview with a persistent tag editor sidebar for sequential precision tagging
- **Project_Configuration**: A modal overlay for configuring project-level settings including root folder, AI model, prefix tags, and workflow stages
- **Zoom_Slider**: A slider control in the Project Overview that adjusts thumbnail size from 100px to 400px
- **Batch_Add_Popup**: A floating autocomplete popup triggered by typing `+` in Project Overview for appending a tag to all images in the active folder or current selection
- **Batch_Remove_Popup**: A floating popup triggered by typing `-` in Project Overview showing existing folder tags with frequencies for removal
- **IMessenger**: The CommunityToolkit.Mvvm messaging interface used for decoupled, screen-agnostic event communication between ViewModels via typed message objects
- **Recycle_Bin**: The operating system's recycle bin (trash), used as the destination when deleting images and their associated Tag_Files

## Requirements

### Requirement 1: Projects Hub Screen

**User Story:** As a dataset curator, I want a central dashboard to view, create, and launch dataset projects, so that I can quickly access and manage all my training datasets from one place.

#### Acceptance Criteria

1. WHEN the Application launches, THE Projects_Hub SHALL display a fluid grid of Project cards on the main screen with a 64px top bar.
2. THE Projects_Hub SHALL display each Project card with the project name, root folder path, total image count, and a progress bar showing the percentage of tagged images.
3. WHEN the user clicks a "New Project" button in the top-right of the top bar, THE Application SHALL create a new Project and open the Project_Configuration modal.
4. WHEN the user selects a Master_Root_Directory via the directory picker in the top bar, THE Projects_Hub SHALL perform a background scan and automatically create Project cards for each discovered subfolder, using the subfolder name as the project name.
5. WHEN no projects exist, THE Projects_Hub SHALL display centered placeholder text "No datasets found. Create your first project or point to a master folder." with a dashed border.
6. WHEN the user hovers over a Project card, THE Projects_Hub SHALL display a 1px solid Primary-colored border on that card.
7. WHEN the user clicks a Project card, THE Application SHALL navigate to the Project_Overview for that Project.

### Requirement 2: Project Overview Screen

**User Story:** As a dataset curator, I want a dense image grid with workflow folder navigation and batch tag operations, so that I can efficiently review, organize, and tag large volumes of images using keyboard shortcuts.

#### Acceptance Criteria

1. THE Project_Overview SHALL display a three-column layout: a 240px left sidebar containing the folder tree, a fluid center area containing the image grid, and a 64px top bar.
2. THE Project_Overview SHALL display the Workflow_Stage folders as a vertically stacked list in the left sidebar, showing pure folder names with numeric prefixes stripped, and an image count for each folder.
3. WHEN the user clicks a Workflow_Stage folder in the sidebar, THE Project_Overview SHALL update the center grid to display images from that folder.
4. THE Project_Overview SHALL display images in an auto-fill grid with a minimum cell size of 160px, cropping images to 1:1 square aspect ratio.
5. THE Project_Overview SHALL display a Status_Dot in the bottom-right corner of each thumbnail: Red for untagged, Yellow for auto-tagged/needs review, Green for ready.
6. WHEN the user hovers over a thumbnail, THE Project_Overview SHALL display a checkbox in the top-left corner of that thumbnail.
7. THE Project_Overview SHALL display the Project name in the top-left of the top bar, an AI model selection dropdown, and a Quick_Filter_Bar input.
8. WHEN the user presses the `/` key, THE Project_Overview SHALL immediately focus the Quick_Filter_Bar for tag-based filtering.
9. WHEN the user presses arrow keys, THE Project_Overview SHALL navigate spatial focus between thumbnails in the grid. THE currently focused thumbnail SHALL be highlighted with a 2px solid frame using the Warning color (`#D79921`) to clearly indicate the active item.
10. WHEN the user presses the `x` key, THE Project_Overview SHALL toggle selection of the currently focused thumbnail.
11. WHEN the user types `+`, THE Project_Overview SHALL open the Batch_Add_Popup with autocomplete, appending the selected tag to all images in the active folder or current selection while skipping duplicates.
12. WHEN the user types `-`, THE Project_Overview SHALL open the Batch_Remove_Popup displaying existing tags in the folder with their frequencies, removing the selected tag from all images in the active folder or current selection.
13. WHEN the user presses `[` or `]`, THE Project_Overview SHALL move the currently selected image(s) to the previous or next Workflow_Stage folder in the sequence.
14. WHEN the user presses `Alt+[` or `Alt+]`, THE Project_Overview SHALL navigate the active folder view to the previous or next Workflow_Stage without moving any images.
15. WHEN the user presses the `Delete` key, THE Project_Overview SHALL send the currently selected image(s) and their associated Tag_Files to the operating system's recycle bin, then auto-advance focus to the next image in the grid.
16. WHEN the user double-clicks a thumbnail, THE Application SHALL transition to Inspector_Mode for that image.
17. WHEN the user presses `Ctrl+Shift+C`, THE Project_Overview SHALL copy the full tag set of the focused image to the clipboard. WHEN the user presses `Ctrl+Shift+V`, THE Project_Overview SHALL paste the clipboard tag set onto the focused image.
18. THE Project_Overview SHALL display a Zoom_Slider in the bottom-right of the grid area, controlling thumbnail size from 100px to 400px.
19. WHILE the AI_Tagger is processing an untagged image, THE Project_Overview SHALL display a spinning processing icon over that thumbnail with reduced opacity.
20. WHEN a folder contains no images, THE Project_Overview SHALL display centered text "Folder is empty. Use stage actions to move images here."
21. THE Project_Overview SHALL display a Hint_Bar at the bottom of the screen showing available keyboard shortcuts for the current context.
22. WHEN the user presses the `Escape` key while a Batch_Add_Popup or Batch_Remove_Popup is open, THE Project_Overview SHALL dismiss the popup. WHEN no popup is open and a TextBox has focus, `Escape` SHALL return focus to the grid.

### Requirement 3: Inspector Mode Screen

**User Story:** As a dataset curator, I want a focused single-image view with persistent tag input and keyboard-driven sequential navigation, so that I can rapidly tag images one by one without breaking my flow.

#### Acceptance Criteria

1. THE Inspector_Mode SHALL display a three-column layout: a 240px left sidebar with Workflow_Stage folders (shared component from Project_Overview), a fluid center pane with the large image preview, and a 320px right sidebar with the tag inspector panel.
2. THE Inspector_Mode SHALL scale the image to fit the center pane while preserving aspect ratio. THE current image SHALL be framed with a 2px solid border using the Warning color (`#D79921`) to indicate it is the active item.
3. THE Inspector_Mode SHALL display a read-only Prefix_Tags block at the top of the right sidebar, showing the project's fixed prefix tags in monospace font.
4. THE Inspector_Mode SHALL display a 32px-height tag input field with persistent focus and an auto-suggest dropdown sourced from the project's Tag_Dictionary.
5. THE Inspector_Mode SHALL display applied tags as a wrapping list of tag pills below the input, each with an `x` button for removal.
6. WHEN the user types any letter key, THE Inspector_Mode SHALL immediately focus the tag input and begin text entry without requiring a mouse click.
7. WHEN the user presses Enter with text in the tag input, THE Inspector_Mode SHALL commit the tag to the current image and auto-advance to the next untagged image (Red or Yellow status).
8. WHEN the user clicks the `x` button on a tag pill, THE Inspector_Mode SHALL remove that tag instantly without confirmation.
9. WHEN the user presses the Left or Right arrow key, THE Inspector_Mode SHALL navigate to the previous or next image with a sliding animation.
10. WHEN the user presses `[` or `]`, THE Inspector_Mode SHALL move the current image to the previous or next Workflow_Stage folder and auto-advance to the next image.
11. WHEN the user presses the `Delete` key, THE Inspector_Mode SHALL send the current image and its associated Tag_File to the operating system's recycle bin, then auto-advance to the next image in the sequence.
12. WHEN the user presses `Ctrl+Shift+C`, THE Inspector_Mode SHALL copy the full tag set to the clipboard. WHEN the user presses `Ctrl+Shift+V`, THE Inspector_Mode SHALL paste the clipboard tag set onto the current image.
13. WHILE the AI_Tagger is generating initial tags for the current image, THE Inspector_Mode SHALL display a spinning indicator over the tag list area.
14. THE Inspector_Mode SHALL display a Hint_Bar showing available keyboard shortcuts at the bottom of the screen.
15. WHEN the user presses the `Escape` key, THE Inspector_Mode SHALL navigate back to the Project_Overview.

### Requirement 4: Project Configuration Modal

**User Story:** As a dataset curator, I want to configure project-level settings including root folder, AI model, prefix tags, and workflow stages, so that I can establish consistent rules for each dataset project.

#### Acceptance Criteria

1. THE Project_Configuration SHALL display as a centered 600px-wide modal overlay with a semi-transparent background wash.
2. THE Project_Configuration SHALL provide a Root_Folder selector with a text input and a browse button for mapping the project to a directory on disk.
3. THE Project_Configuration SHALL provide an AI model dropdown for selecting the background tagging model from the shared `ai_models.json` catalog, and SHALL expose a `Download Model` button beside the selector for Hugging Face-backed models that are not yet installed.
4. THE Project_Configuration SHALL provide a Prefix_Tags editor textarea for defining fixed keyword prefixes prepended to all tag files, with comma-separated format.
5. IF the Prefix_Tags editor contains invalid characters, THEN THE Project_Configuration SHALL display a Red error border on the textarea and show a descriptive error message below the field.
6. THE Project_Configuration SHALL provide a Workflow Stages builder displaying a draggable, reorderable list of stage folder names with inline editing, delete buttons, and an "Add Stage" button.
7. WHEN the user clicks the Save button, THE Project_Configuration SHALL close the modal and instantly apply all settings to the current session.

### Requirement 5: Tags Overview Screen

**User Story:** As a dataset curator, I want a centralized tag taxonomy manager with alias support, frequency tracking, and inline editing, so that I can standardize tags across all my projects and resolve inconsistencies.

#### Acceptance Criteria

1. THE Tags_Overview screen SHALL display a two-column layout: a 240px left sidebar with category filters (All Tags, Needs Alias, Orphaned Tags, Frequent Tags) and a fluid center pane with a sortable DataGrid.
2. THE Tags_Overview SHALL display a top-anchored search/filter bar for finding tags across all datasets.
3. THE Tags_Overview DataGrid SHALL display sortable columns for Tag Name, Alias, Global Frequency, and Actions (Edit, Merge, Delete).
4. WHEN the user double-clicks a DataGrid row, THE Tags_Overview SHALL enter inline edit mode for that row, allowing renaming of the tag and configuration of aliases.
5. WHEN the user triggers a Merge action on a tag, THE Tags_Overview SHALL merge the selected tag into a target tag, updating all references across all projects.
6. WHEN the user triggers a Delete action on a tag, THE Tags_Overview SHALL remove the tag from the dictionary and offer to remove the tag from all associated Tag_Files.
7. THE Tags_Overview SHALL display tag names in monospace font.

### Requirement 6: Tag File Storage Format

**User Story:** As a dataset curator, I want tags stored as plain text files alongside images in Booru-style comma-separated format, so that the tag data is portable, human-readable, and compatible with standard AI training tools.

#### Acceptance Criteria

1. THE Application SHALL store tags for each image in a Tag_File with the same base filename as the image and a `.txt` extension, located in the same directory as the image.
2. THE Application SHALL write tags in comma-separated format within each Tag_File.
3. WHEN saving tags, THE Application SHALL prepend the project's Prefix_Tags to the beginning of the tag list in the Tag_File.
4. WHEN the Application reads a Tag_File, THE Application SHALL parse the comma-separated tags and display them individually. WHEN the Application writes a Tag_File, THE Application SHALL serialize the tag list back to comma-separated format. FOR ALL valid tag lists, reading then writing then reading a Tag_File SHALL produce an equivalent tag list (round-trip property).

### Requirement 7: Background AI Auto-Tagging

**User Story:** As a dataset curator, I want untagged images to be automatically tagged by a background AI process, so that I have a starting point for review rather than tagging every image from scratch.

#### Acceptance Criteria

1. WHEN an image in a Project has no associated Tag_File, THE AI_Tagger SHALL automatically queue that image for background tag generation using the configured AI model.
2. WHILE the AI_Tagger is processing an image, THE Application SHALL display a visual processing indicator on that image's thumbnail in the Project_Overview and in the Inspector_Mode tag area.
3. WHEN the AI_Tagger completes tag generation for an image, THE Application SHALL create a Tag_File for that image and set the image's status to Yellow (auto-tagged/needs review).
4. THE AI_Tagger SHALL read the available model catalog from `ai_models.json`, and the user SHALL select the active model via the Project_Configuration modal or the Project_Overview top bar dropdown.
5. FOR model-catalog entries that specify a Hugging Face repository, THE Application SHALL download the selected model only when the user activates the explicit `Download Model` button beside the AI model selector. Background tagging SHALL NOT trigger model installation automatically.
6. FOR supported local ONNX taggers, THE AI_Tagger SHALL keep one long-lived ONNX Runtime GPU session per active model, SHALL inspect model metadata at runtime, and SHALL batch multiple images into each inference call instead of reloading the model per image.
7. FOR WD-style ONNX taggers, THE AI_Tagger SHALL load `selected_tags.csv` once per active model, SHALL store structured `rating`, `general`, and `character` tag results internally, and SHALL derive flat `.txt` training sidecars from the accepted tags after inference.

### Requirement 8: Workflow Stage Folder Management

**User Story:** As a dataset curator, I want workflow stages represented as physical subfolders that I can move images between using keyboard shortcuts or commands, so that I can track each image's progress through my curation pipeline.

#### Acceptance Criteria

1. THE Application SHALL represent each Workflow_Stage as a physical subfolder within the Project's Root_Folder on disk.
2. THE Application SHALL determine the sequential order of Workflow_Stages dynamically by parsing the numeric prefix from each subfolder name (e.g., `01_Inbox` → order 1, `02_Review` → order 2). The Application SHALL NOT hardcode folder order; it SHALL always derive ordering from the on-disk numeric prefixes so that stages can be reordered, added, or removed in the future without code changes.
3. WHEN the user moves an image between Workflow_Stages (via keyboard shortcut or command), THE Application SHALL physically move the image file and its associated Tag_File to the target Workflow_Stage subfolder on disk.
4. THE Application SHALL display Workflow_Stage folder names in the sidebar with numeric prefixes stripped from the display.
5. WHEN a new Project is created with defined Workflow_Stages, THE Application SHALL create the corresponding subfolders on disk if they do not already exist.

### Requirement 9: Keyboard Accessibility and Navigation

**User Story:** As a dataset curator, I want comprehensive keyboard shortcuts across all screens, so that I can perform all core operations without reaching for the mouse.

#### Acceptance Criteria

1. THE Application SHALL provide keyboard shortcuts for all core operations: tag addition, tag removal, image navigation, folder navigation, image selection, image deletion, image movement between Workflow_Stages, popup dismissal (`Escape`), and back navigation (`Escape` from Inspector_Mode).
2. THE Application SHALL display a Hint_Bar at the bottom of each screen showing the available keyboard shortcuts for the current context.
3. THE Application SHALL display a Status_Bar at the bottom of each screen for feedback messages and contextual status information (display only, no input).
4. WHEN the user presses `/` on any screen with a filter bar, THE Application SHALL immediately focus the filter input.
5. THE Application SHALL support `Ctrl+Shift+C` and `Ctrl+Shift+V` for copying and pasting full tag sets between images on both Project_Overview and Inspector_Mode screens.

### Requirement 10: Design System and Visual Identity

**User Story:** As a developer, I want a clearly defined design system with Gruvbox Light tokens, IBM Plex typography, and consistent component styling, so that the application has a cohesive warm, retro-utilitarian aesthetic across all screens.

#### Acceptance Criteria

1. THE Application SHALL use the Gruvbox Light color palette with a calmer desktop primary accent: Background `#FBF1C7`, Surface `#EBDBB2`, Surface Elevated `#D5C4A1`, Primary `#B57614`, Text `#3C3836`, Muted `#7C6F64`, Accent `#98971A`, Warning `#D79921`, Error `#CC241D`.
2. THE Application SHALL use IBM Plex Sans for headings (600 weight, 18-24px), body text (400 weight, 13px), and buttons (500 weight, 12px, uppercase, 0.5px tracking). THE Application SHALL use IBM Plex Mono for tags, metadata, and code inputs (500 weight, 12px).
3. THE Application SHALL use hard 2px border radiuses, sharp 1px borders, and 1px solid Primary-colored borders for active states instead of background color changes. THE Application SHALL use a 2px solid Warning-colored (`#D79921`) border to indicate the currently focused/active item in grids and image views.
4. THE Application SHALL define XAML ResourceDictionaries containing all design tokens including colors, fonts, spacing (4px, 8px, 16px, 24px), and border styles for consistent reuse across all screens.
5. THE Application SHALL define an active focus frame style as a 2px solid Warning `#D79921` border applied to the currently focused or active item (thumbnail in Project_Overview, image preview in Inspector_Mode) to provide clear visual identification of the active element.

### Requirement 11: State Persistence

**User Story:** As a dataset curator, I want all application state and configuration to be automatically saved and restored between sessions, so that I never lose my working context when I close and reopen the application.

#### Acceptance Criteria

1. THE Application SHALL persist all configurable settings and stateful properties to disk on change, including but not limited to: project configurations, last-opened project, active workflow stage, zoom slider position, window size/position, selected AI model, prefix tags, tag dictionary entries, and any user preferences.
2. WHEN the Application launches, THE Application SHALL restore all previously persisted state so that the user resumes exactly where they left off.
3. THE Application SHALL persist state for every property that can be configured or modified by the user. No stateful property SHALL be excluded from persistence.
4. THE Application SHALL use the `.datasetstudio.json` project configuration file for project-level state and a separate application-level settings file for global state (window geometry, last project, etc.).

### Requirement 12: Event-Driven Architecture

**User Story:** As a developer, I want all cross-component communication to flow through a centralized event bus with screen-agnostic messages, so that screens remain decoupled and side effects are handled consistently.

#### Acceptance Criteria

1. THE Application SHALL use the CommunityToolkit.Mvvm `IMessenger` as the sole mechanism for cross-ViewModel communication. ViewModels SHALL NOT reference or call methods on other ViewModels directly.
2. ALL application events (image moved, image deleted, tags changed, stage changed, AI tagging completed, etc.) SHALL be defined as screen-agnostic message types. Messages SHALL NOT contain screen-specific logic or reference specific ViewModels.
3. ALL side effects triggered by data changes (e.g., refreshing folder image counts, updating status dots, recalculating tag frequencies, updating progress bars) SHALL occur as reactions to messenger events or through Avalonia data binding. Side effects SHALL NEVER be performed by directly manipulating View elements from code-behind or ViewModel logic.
4. WHEN an image is moved, deleted, tagged, or otherwise modified, THE originating ViewModel SHALL publish a single event. All other ViewModels or services that need to react SHALL subscribe to that event independently.

### Requirement 13: MVVM Discipline

**User Story:** As a developer, I want strict MVVM enforcement rules documented and followed, so that the codebase remains maintainable, testable, and free of architectural shortcuts.

#### Acceptance Criteria

1. ALL ViewModel observable properties SHALL be declared using CommunityToolkit.Mvvm `[ObservableProperty]` attribute for auto-generated property implementations. Manual `INotifyPropertyChanged` implementations SHALL NOT be used where the attribute is applicable.
2. THE data flow SHALL strictly follow: Model → ViewModel → View for data updates, and View → ViewModel → Model for user commands. Models SHALL NEVER interact with Views directly.
3. ALL service operations that perform I/O (file system, AI model inference, JSON read/write) SHALL be asynchronous (`async Task`). No synchronous blocking calls SHALL be made on the UI thread.
4. View code-behind SHALL contain ONLY view-specific logic (focus management, animation triggers). All business logic and state management SHALL reside in ViewModels and Services.

### Requirement 14: Supported Image Formats

**User Story:** As a dataset curator, I want the application to read common image formats and save only in WebP, so that my datasets are stored in a modern, efficient format.

#### Acceptance Criteria

1. THE Application SHALL read and display images in the following formats: PNG (`.png`), JPEG (`.jpg`, `.jpeg`), WebP (`.webp`), and BMP (`.bmp`).
2. THE Application SHALL generate cached thumbnails in WebP (`.webp`) format for optimal size and performance.
3. THE Application SHALL treat any file with a recognized image extension in a Workflow_Stage folder as a valid image entry.

### Requirement 15: Caching

**User Story:** As a dataset curator working with thousands of images, I want the application to cache thumbnails and frequently accessed data so that navigation and browsing remain fast and responsive.

#### Acceptance Criteria

1. THE Application SHALL generate and cache thumbnail images for the Project_Overview on first load, storing them in a `.datasetstudio-cache/` subfolder within the Project's Root_Folder.
2. WHEN a cached thumbnail exists and the source image has not been modified (based on file modification timestamp), THE Application SHALL load the thumbnail from cache instead of re-reading and resizing the full image.
3. WHEN the source image is modified or replaced, THE Application SHALL invalidate and regenerate the cached thumbnail on next access.
4. THE Application SHALL cache the Tag_Dictionary in memory for the active project to avoid repeated disk reads during autocomplete and frequency lookups.
5. THE Application SHALL cache the parsed Workflow_Stage folder list in memory, refreshing it when a `FileSystemWatcher` detects changes to the project's root folder structure.
