using System.Collections.Generic;
using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DatasetStudio.ViewModels;

namespace DatasetStudio.Views;

public partial class ProjectOverviewView : ScreenViewBase<ProjectOverviewViewModel>
{
    private ProjectOverviewViewModel? observedViewModel;

    public ProjectOverviewView()
    {
        InitializeComponent();
        DataContextChanged += OnProjectOverviewDataContextChanged;
        DetachedFromVisualTree += OnProjectOverviewDetachedFromVisualTree;
        ImageRowsListBox.SizeChanged += OnImageRowsListBoxSizeChanged;
    }

    protected override IReadOnlyList<ScreenShortcut> BuildScreenShortcuts()
    {
        ProjectOverviewViewModel? viewModel = ViewModel;
        if (viewModel is null)
        {
            return [];
        }

        return new ScreenShortcut[]
        {
            CreateSlashShortcut("Filter", FocusFilterTextBox),
            new ScreenShortcut(MatchesBatchAddShortcut, "+", "Batch Add", OpenBatchAddPopup, isAvailable: HasImagesAvailable),
            new ScreenShortcut(MatchesBatchRemoveShortcut, "-", "Batch Remove", OpenBatchRemovePopup, isAvailable: HasImagesAvailable),
            CreateShortcut(
                Key.Enter,
                KeyModifiers.None,
                "Enter",
                "Apply Batch Tag",
                CommitOpenBatchPopup,
                allowWhenTextInputFocused: true,
                isAvailable: IsAnyBatchPopupOpen),
            CreateShortcut(
                Key.Escape,
                KeyModifiers.None,
                "Esc",
                "Close Popup",
                CloseOpenBatchPopup,
                allowWhenTextInputFocused: true,
                isAvailable: IsAnyBatchPopupOpen),
            CreateShortcut(
                Key.T,
                KeyModifiers.Control,
                "Ctrl+T",
                "Tags Overview",
                () => viewModel.OpenTagsOverviewCommand.Execute(null),
                allowWhenTextInputFocused: true),
            CreateShortcut(
                Key.X,
                KeyModifiers.None,
                "X",
                "Select",
                ToggleFocusedSelection,
                isAvailable: HasFocusedImage),
            CreateShortcut(
                Key.Left,
                KeyModifiers.None,
                "Left",
                "Focus Left",
                () => NavigateGrid(-1),
                isAvailable: HasFocusedImage),
            CreateShortcut(
                Key.Right,
                KeyModifiers.None,
                "Right",
                "Focus Right",
                () => NavigateGrid(1),
                isAvailable: HasFocusedImage),
            CreateShortcut(
                Key.Up,
                KeyModifiers.None,
                "Up",
                "Focus Up",
                () => NavigateGrid(-GetItemsPerRowEstimate()),
                isAvailable: HasFocusedImage),
            CreateShortcut(
                Key.Down,
                KeyModifiers.None,
                "Down",
                "Focus Down",
                () => NavigateGrid(GetItemsPerRowEstimate()),
                isAvailable: HasFocusedImage),
            CreateShortcut(
                Key.Oem4,
                KeyModifiers.None,
                "[",
                "Move Prev Stage",
                () => viewModel.MoveImageCommand.Execute(-1),
                isAvailable: HasSelectedImages),
            CreateShortcut(
                Key.Oem6,
                KeyModifiers.None,
                "]",
                "Move Next Stage",
                () => viewModel.MoveImageCommand.Execute(1),
                isAvailable: HasSelectedImages),
            CreateShortcut(
                Key.Oem4,
                KeyModifiers.Alt,
                "Alt+[",
                "View Prev Stage",
                () => viewModel.NavigateStageCommand.Execute(-1),
                isAvailable: CanNavigateToPreviousStage),
            CreateShortcut(
                Key.Oem6,
                KeyModifiers.Alt,
                "Alt+]",
                "View Next Stage",
                () => viewModel.NavigateStageCommand.Execute(1),
                isAvailable: CanNavigateToNextStage),
            CreateShortcut(
                Key.Delete,
                KeyModifiers.None,
                "Del",
                "Recycle Selected",
                () => viewModel.DeleteImageCommand.Execute(null),
                isAvailable: HasSelectedImages),
            CreateShortcut(
                Key.C,
                KeyModifiers.Control | KeyModifiers.Shift,
                "Ctrl+Shift+C",
                "Copy Tags",
                () => viewModel.CopyTagsCommand.Execute(null),
                allowWhenTextInputFocused: true,
                isAvailable: HasFocusedImage),
            CreateShortcut(
                Key.V,
                KeyModifiers.Control | KeyModifiers.Shift,
                "Ctrl+Shift+V",
                "Paste Tags",
                () => viewModel.PasteTagsCommand.Execute(null),
                allowWhenTextInputFocused: true,
                isAvailable: HasFocusedImage),
            CreateShortcut(
                Key.Enter,
                KeyModifiers.None,
                "Enter",
                "Inspect",
                OpenFocusedImage,
                isAvailable: () => !IsAnyBatchPopupOpen() && HasFocusedImage()),
        };
    }

    protected override bool ShouldOfferLeaveFieldShortcut()
    {
        if (IsAnyBatchPopupOpen())
        {
            return false;
        }

        return base.ShouldOfferLeaveFieldShortcut();
    }

    private void FocusFilterTextBox()
    {
        FocusControl(FilterTextBox);
    }

    private void OpenFocusedImage()
    {
        ViewModel?.OpenInspectorCommand.Execute(null);
    }

    private bool HasFocusedImage()
    {
        return ViewModel is not null && ViewModel.FocusedImageIndex >= 0 && ViewModel.FocusedImageIndex < ViewModel.Images.Count;
    }

    private bool HasImagesAvailable()
    {
        return ViewModel is not null && ViewModel.HasImages;
    }

    private bool HasSelectedImages()
    {
        return ViewModel is not null && ViewModel.SelectedImages.Count > 0;
    }

    private bool IsAnyBatchPopupOpen()
    {
        return ViewModel is not null && (ViewModel.IsBatchAddOpen || ViewModel.IsBatchRemoveOpen);
    }

    private bool CanNavigateToPreviousStage()
    {
        if (ViewModel?.ActiveStage is null)
        {
            return false;
        }

        return ViewModel.Stages.IndexOf(ViewModel.ActiveStage) > 0;
    }

    private bool CanNavigateToNextStage()
    {
        if (ViewModel?.ActiveStage is null)
        {
            return false;
        }

        int activeStageIndex = ViewModel.Stages.IndexOf(ViewModel.ActiveStage);
        return activeStageIndex >= 0 && activeStageIndex < ViewModel.Stages.Count - 1;
    }

    private void ToggleFocusedSelection()
    {
        if (ViewModel is null || !HasFocusedImage())
        {
            return;
        }

        ViewModel.ToggleSelectionCommand.Execute(ViewModel.Images[ViewModel.FocusedImageIndex]);
    }

    private void NavigateGrid(int offset)
    {
        if (ViewModel is null || offset == 0)
        {
            return;
        }

        ViewModel.NavigateGridCommand.Execute(offset);
        BringFocusedImageIntoView();
    }

    private void OpenBatchAddPopup()
    {
        if (ViewModel is null)
        {
            return;
        }

        ViewModel.OpenBatchAddCommand.Execute(null);
        Dispatcher.UIThread.Post(BatchAddPopup.FocusQueryTextBox, DispatcherPriority.Input);
    }

    private void OpenBatchRemovePopup()
    {
        if (ViewModel is null)
        {
            return;
        }

        ViewModel.OpenBatchRemoveCommand.Execute(null);
        Dispatcher.UIThread.Post(BatchRemovePopup.FocusQueryTextBox, DispatcherPriority.Input);
    }

    private void CommitOpenBatchPopup()
    {
        if (ViewModel is null)
        {
            return;
        }

        if (ViewModel.IsBatchAddOpen)
        {
            ViewModel.CommitBatchAddCommand.Execute(null);
            return;
        }

        if (ViewModel.IsBatchRemoveOpen)
        {
            ViewModel.CommitBatchRemoveCommand.Execute(null);
        }
    }

    private void CloseOpenBatchPopup()
    {
        if (ViewModel is null)
        {
            return;
        }

        if (ViewModel.IsBatchAddOpen)
        {
            ViewModel.CloseBatchAddCommand.Execute(null);
            return;
        }

        if (ViewModel.IsBatchRemoveOpen)
        {
            ViewModel.CloseBatchRemoveCommand.Execute(null);
        }
    }

    private static bool MatchesBatchAddShortcut(KeyEventArgs eventArgs)
    {
        return (eventArgs.Key == Key.OemPlus && eventArgs.KeyModifiers == KeyModifiers.Shift)
            || (eventArgs.Key == Key.Add && eventArgs.KeyModifiers == KeyModifiers.None);
    }

    private static bool MatchesBatchRemoveShortcut(KeyEventArgs eventArgs)
    {
        return (eventArgs.Key == Key.OemMinus && eventArgs.KeyModifiers == KeyModifiers.None)
            || (eventArgs.Key == Key.Subtract && eventArgs.KeyModifiers == KeyModifiers.None);
    }

    private int GetItemsPerRowEstimate()
    {
        if (ViewModel is null)
        {
            return 1;
        }

        if (ViewModel.ItemsPerRow > 0)
        {
            return ViewModel.ItemsPerRow;
        }

        double usableWidth = ImageRowsListBox.Bounds.Width;
        if (usableWidth <= 0)
        {
            return 1;
        }

        double estimatedTileWidth = ViewModel.ZoomValue + 32;
        if (estimatedTileWidth <= 0)
        {
            return 1;
        }

        int itemsPerRow = (int)(usableWidth / estimatedTileWidth);
        if (itemsPerRow < 1)
        {
            return 1;
        }

        return itemsPerRow;
    }

    private void BringFocusedImageIntoView()
    {
        if (ViewModel is null || !HasFocusedImage())
        {
            return;
        }

        int itemsPerRow = ViewModel.ItemsPerRow;
        if (itemsPerRow < 1)
        {
            itemsPerRow = 1;
        }

        int targetRowIndex = ViewModel.FocusedImageIndex / itemsPerRow;
        if (targetRowIndex >= 0 && targetRowIndex < ViewModel.ImageRows.Count)
        {
            ImageRowsListBox.ScrollIntoView(ViewModel.ImageRows[targetRowIndex]);
        }

        Dispatcher.UIThread.Post(BringRealizedFocusedImageIntoView, DispatcherPriority.Background);
    }

    private void BringRealizedFocusedImageIntoView()
    {
        if (ViewModel is null || !HasFocusedImage())
        {
            return;
        }

        ProjectOverviewImageViewModel focusedImage = ViewModel.Images[ViewModel.FocusedImageIndex];
        foreach (Avalonia.Visual visual in this.GetVisualDescendants())
        {
            if (visual is not Control control)
            {
                continue;
            }

            if (!ReferenceEquals(control.DataContext, focusedImage))
            {
                continue;
            }

            control.BringIntoView();
            return;
        }
    }

    private void OnProjectOverviewDataContextChanged(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        AttachObservedViewModel(ViewModel);
        Dispatcher.UIThread.Post(UpdateItemsPerRowEstimate, DispatcherPriority.Loaded);
    }

    private void OnProjectOverviewDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        AttachObservedViewModel(null);
    }

    private void OnObservedViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.PropertyName == nameof(ProjectOverviewViewModel.ZoomValue)
            || eventArgs.PropertyName == nameof(ProjectOverviewViewModel.HasImages))
        {
            Dispatcher.UIThread.Post(UpdateItemsPerRowEstimate, DispatcherPriority.Loaded);
        }
    }

    private void AttachObservedViewModel(ProjectOverviewViewModel? viewModel)
    {
        if (ReferenceEquals(observedViewModel, viewModel))
        {
            return;
        }

        if (observedViewModel is not null)
        {
            observedViewModel.PropertyChanged -= OnObservedViewModelPropertyChanged;
        }

        observedViewModel = viewModel;
        if (observedViewModel is not null)
        {
            observedViewModel.PropertyChanged += OnObservedViewModelPropertyChanged;
        }
    }

    private void OnImageRowsListBoxSizeChanged(object? sender, SizeChangedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        UpdateItemsPerRowEstimate();
    }

    private void UpdateItemsPerRowEstimate()
    {
        if (ViewModel is null)
        {
            return;
        }

        int itemsPerRow = CalculateItemsPerRow();
        if (itemsPerRow == ViewModel.ItemsPerRow)
        {
            return;
        }

        ViewModel.ItemsPerRow = itemsPerRow;
    }

    private int CalculateItemsPerRow()
    {
        if (ViewModel is null)
        {
            return 1;
        }

        double usableWidth = ImageRowsListBox.Bounds.Width;
        if (usableWidth <= 0)
        {
            return 1;
        }

        double estimatedTileWidth = ViewModel.ZoomValue + 32;
        if (estimatedTileWidth <= 0)
        {
            return 1;
        }

        int itemsPerRow = (int)(usableWidth / estimatedTileWidth);
        if (itemsPerRow < 1)
        {
            return 1;
        }

        return itemsPerRow;
    }

    private void OnThumbnailDoubleTapped(object? sender, RoutedEventArgs eventArgs)
    {
        _ = eventArgs;

        if (sender is not InputElement inputElement)
        {
            return;
        }

        if (inputElement.DataContext is not ProjectOverviewImageViewModel image)
        {
            return;
        }

        ViewModel?.OpenInspectorCommand.Execute(image);
    }
}
