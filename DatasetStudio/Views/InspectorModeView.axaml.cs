using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using DatasetStudio.ViewModels;

namespace DatasetStudio.Views;

public partial class InspectorModeView : ScreenViewBase<ScreenViewModelBase>
{
    public InspectorModeView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    protected override IReadOnlyList<ScreenShortcut> BuildScreenShortcuts()
    {
        return new ScreenShortcut[]
        {
            CreateSlashShortcut("Focus Tag Input", FocusTagInputTextBox, allowWhenTextInputFocused: true),
            CreateShortcut(
                Key.Enter,
                KeyModifiers.None,
                "Enter",
                "Commit Tag",
                CommitTag,
                allowWhenTextInputFocused: true,
                isAvailable: HasCommitTagCommand),
            CreateShortcut(
                Key.Left,
                KeyModifiers.None,
                "Left",
                "Previous Image",
                NavigatePreviousImage,
                isAvailable: HasNavigatePreviousCommand),
            CreateShortcut(
                Key.Right,
                KeyModifiers.None,
                "Right",
                "Next Image",
                NavigateNextImage,
                isAvailable: HasNavigateNextCommand),
            CreateShortcut(
                Key.C,
                KeyModifiers.Control | KeyModifiers.Shift,
                "Ctrl+Shift+C",
                "Copy Tags",
                CopyTags,
                allowWhenTextInputFocused: true,
                isAvailable: HasCopyTagsCommand),
            CreateShortcut(
                Key.V,
                KeyModifiers.Control | KeyModifiers.Shift,
                "Ctrl+Shift+V",
                "Paste Tags",
                PasteTags,
                allowWhenTextInputFocused: true,
                isAvailable: HasPasteTagsCommand),
            CreateShortcut(
                Key.Escape,
                KeyModifiers.None,
                "Esc",
                "Go Back",
                GoBack,
                isAvailable: () => !HasEditableTextInputFocus()),
        };
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

    private void CommitTag()
    {
        ExecuteNamedCommand("CommitTagCommand");
    }

    private void NavigatePreviousImage()
    {
        ExecuteNamedCommand("NavigatePreviousCommand");
    }

    private void NavigateNextImage()
    {
        ExecuteNamedCommand("NavigateNextCommand");
    }

    private void CopyTags()
    {
        ExecuteNamedCommand("CopyTagsCommand");
    }

    private void PasteTags()
    {
        ExecuteNamedCommand("PasteTagsCommand");
    }

    private void GoBack()
    {
        ExecuteNamedCommand("GoBackCommand");
    }

    private bool HasCommitTagCommand()
    {
        return HasNamedCommand("CommitTagCommand");
    }

    private bool HasNavigatePreviousCommand()
    {
        return HasNamedCommand("NavigatePreviousCommand");
    }

    private bool HasNavigateNextCommand()
    {
        return HasNamedCommand("NavigateNextCommand");
    }

    private bool HasCopyTagsCommand()
    {
        return HasNamedCommand("CopyTagsCommand");
    }

    private bool HasPasteTagsCommand()
    {
        return HasNamedCommand("PasteTagsCommand");
    }

    private bool HasNamedCommand(string propertyName)
    {
        return GetNamedCommand(propertyName) is not null;
    }

    private ICommand? GetNamedCommand(string propertyName)
    {
        ScreenViewModelBase? viewModel = ViewModel;
        if (viewModel is null)
        {
            return null;
        }

        PropertyInfo? propertyInfo = viewModel.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        if (propertyInfo is null)
        {
            return null;
        }

        if (propertyInfo.GetValue(viewModel) is not ICommand command)
        {
            return null;
        }

        return command;
    }

    private void ExecuteNamedCommand(string propertyName)
    {
        ICommand? command = GetNamedCommand(propertyName);
        if (command is null)
        {
            return;
        }

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
