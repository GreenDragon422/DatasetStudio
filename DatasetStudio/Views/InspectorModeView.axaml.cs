using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using DatasetStudio.ViewModels;

namespace DatasetStudio.Views;

public partial class InspectorModeView : ScreenViewBase<InspectorModeViewModel>
{
    private InspectorModeViewModel? observedViewModel;

    public InspectorModeView()
    {
        InitializeComponent();
        DataContextChanged += OnInspectorDataContextChanged;
    }

    protected override IReadOnlyList<ScreenShortcut> BuildScreenShortcuts()
    {
        InspectorModeViewModel? viewModel = ViewModel;
        if (viewModel is null)
        {
            return Array.Empty<ScreenShortcut>();
        }

        return new ScreenShortcut[]
        {
            CreateSlashShortcut("Focus Tag Input", FocusTagInputTextBox, allowWhenTextInputFocused: true),
            CreateShortcut(
                Key.Enter,
                KeyModifiers.None,
                "Enter",
                "Commit & Next",
                () => ExecuteCommand(viewModel.CommitTagCommand),
                allowWhenTextInputFocused: true,
                isAvailable: () => viewModel.CurrentImage is not null),
            CreateShortcut(
                Key.Left,
                KeyModifiers.None,
                "Left",
                "Previous Image",
                () => ExecuteCommand(viewModel.NavigatePreviousCommand),
                isAvailable: () => viewModel.CurrentIndex > 0),
            CreateShortcut(
                Key.Right,
                KeyModifiers.None,
                "Right",
                "Next Image",
                () => ExecuteCommand(viewModel.NavigateNextCommand),
                isAvailable: () => viewModel.CurrentIndex >= 0 && viewModel.CurrentIndex < viewModel.ImageList.Count - 1),
            CreateShortcut(
                Key.Oem4,
                KeyModifiers.None,
                "[",
                "Prev Stage",
                () => ExecuteCommand(viewModel.MoveToPreviousStageCommand),
                isAvailable: () => viewModel.ActiveStage is not null && viewModel.Stages.IndexOf(viewModel.ActiveStage) > 0),
            CreateShortcut(
                Key.Oem6,
                KeyModifiers.None,
                "]",
                "Next Stage",
                () => ExecuteCommand(viewModel.MoveToNextStageCommand),
                isAvailable: () =>
                {
                    if (viewModel.ActiveStage is null)
                    {
                        return false;
                    }

                    int activeStageIndex = viewModel.Stages.IndexOf(viewModel.ActiveStage);
                    return activeStageIndex >= 0 && activeStageIndex < viewModel.Stages.Count - 1;
                }),
            CreateShortcut(
                Key.Delete,
                KeyModifiers.None,
                "Del",
                "Recycle",
                () => ExecuteCommand(viewModel.DeleteImageCommand),
                isAvailable: () => viewModel.CurrentImage is not null),
            CreateShortcut(
                Key.C,
                KeyModifiers.Control | KeyModifiers.Shift,
                "Ctrl+Shift+C",
                "Copy Tags",
                () => ExecuteCommand(viewModel.CopyTagsCommand),
                allowWhenTextInputFocused: true,
                isAvailable: () => viewModel.CurrentImage is not null),
            CreateShortcut(
                Key.V,
                KeyModifiers.Control | KeyModifiers.Shift,
                "Ctrl+Shift+V",
                "Paste Tags",
                () => ExecuteCommand(viewModel.PasteTagsCommand),
                allowWhenTextInputFocused: true,
                isAvailable: () => viewModel.CurrentImage is not null),
            CreateShortcut(
                Key.Escape,
                KeyModifiers.None,
                "Esc",
                "Back",
                () => ExecuteCommand(viewModel.GoBackCommand),
                allowWhenTextInputFocused: true),
        };
    }

    protected override bool ShouldOfferLeaveFieldShortcut()
    {
        return false;
    }

    private void OnLoaded(object? sender, RoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        Dispatcher.UIThread.Post(FocusTagInputTextBox, DispatcherPriority.Input);
    }

    private void OnScreenTextInput(object? sender, TextInputEventArgs eventArgs)
    {
        _ = sender;

        if (TagInputTextBox.IsKeyboardFocusWithin)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(eventArgs.Text))
        {
            return;
        }

        if (!char.IsLetter(eventArgs.Text[0]))
        {
            return;
        }

        FocusTagInputTextBox();
        string currentText = TagInputTextBox.Text ?? string.Empty;
        TagInputTextBox.Text = string.Concat(currentText, eventArgs.Text);
        TagInputTextBox.CaretIndex = TagInputTextBox.Text.Length;
        eventArgs.Handled = true;
    }

    private void OnInspectorDataContextChanged(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        AttachObservedViewModel(ViewModel);
    }

    private void OnObservedViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.PropertyName == nameof(InspectorModeViewModel.CurrentImage))
        {
            Dispatcher.UIThread.Post(FocusTagInputTextBox, DispatcherPriority.Input);
        }
    }

    private void AttachObservedViewModel(InspectorModeViewModel? viewModel)
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

    private static void ExecuteCommand(ICommand command)
    {
        if (command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    private void FocusTagInputTextBox()
    {
        FocusControl(TagInputTextBox);
    }
}
