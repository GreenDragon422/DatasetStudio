using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using DatasetStudio.ViewModels;

namespace DatasetStudio.Views;

public abstract class ScreenViewBase<TViewModel> : UserControl, IScreenView
    where TViewModel : ScreenViewModelBase
{
    private TViewModel? observedViewModel;

    protected ScreenViewBase()
    {
        Focusable = true;
        DataContextChanged += OnScreenDataContextChanged;
        AttachedToVisualTree += OnScreenAttachedToVisualTree;
        DetachedFromVisualTree += OnScreenDetachedFromVisualTree;
        AddHandler(InputElement.GotFocusEvent, OnScreenGotFocus, RoutingStrategies.Bubble);
        AddHandler(InputElement.LostFocusEvent, OnScreenLostFocus, RoutingStrategies.Bubble);
    }

    public TViewModel? ViewModel
    {
        get
        {
            return DataContext as TViewModel;
        }
    }

    ScreenViewModelBase? IScreenView.ScreenViewModel
    {
        get
        {
            return ViewModel;
        }
    }

    public bool TryHandleKey(KeyEventArgs eventArgs)
    {
        ScreenShortcut? shortcut = FindMatchingShortcut(eventArgs);
        if (shortcut is null)
        {
            return false;
        }

        shortcut.Execute();
        eventArgs.Handled = true;
        RefreshShortcutHint();
        return true;
    }

    protected virtual IReadOnlyList<ScreenShortcut> BuildScreenShortcuts()
    {
        return Array.Empty<ScreenShortcut>();
    }

    protected ScreenShortcut CreateShortcut(
        Key key,
        KeyModifiers keyModifiers,
        string gestureText,
        string description,
        Action executeAction,
        bool allowWhenTextInputFocused = false,
        Func<bool>? isAvailable = null)
    {
        return new ScreenShortcut(
            eventArgs => eventArgs.Key == key && eventArgs.KeyModifiers == keyModifiers,
            gestureText,
            description,
            executeAction,
            allowWhenTextInputFocused,
            isAvailable);
    }

    protected ScreenShortcut CreateSlashShortcut(
        string description,
        Action executeAction,
        bool allowWhenTextInputFocused = false,
        Func<bool>? isAvailable = null)
    {
        return new ScreenShortcut(
            MatchesSlashShortcut,
            "/",
            description,
            executeAction,
            allowWhenTextInputFocused,
            isAvailable);
    }

    protected void FocusControl(Control control)
    {
        bool didFocus = control.Focus(NavigationMethod.Tab, KeyModifiers.None);
        _ = didFocus;
    }

    protected bool HasEditableTextInputFocus()
    {
        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        IInputElement? focusedElement = topLevel?.FocusManager?.GetFocusedElement();
        if (focusedElement is not Visual focusedVisual)
        {
            return false;
        }

        foreach (Visual visual in focusedVisual.GetSelfAndVisualAncestors())
        {
            if (visual is TextBox)
            {
                return true;
            }
        }

        return false;
    }

    protected bool IsControlOrDescendantFocused(Control control)
    {
        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        IInputElement? focusedElement = topLevel?.FocusManager?.GetFocusedElement();
        if (focusedElement is not Visual focusedVisual)
        {
            return false;
        }

        foreach (Visual visual in focusedVisual.GetSelfAndVisualAncestors())
        {
            if (ReferenceEquals(visual, control))
            {
                return true;
            }
        }

        return false;
    }

    protected void RefreshShortcutHint()
    {
        TViewModel? viewModel = ViewModel;
        if (viewModel is null)
        {
            return;
        }

        IReadOnlyList<ScreenShortcut> visibleShortcuts = GetVisibleShortcuts();
        if (visibleShortcuts.Count == 0)
        {
            viewModel.HintText = string.Empty;
            return;
        }

        string[] hintSegments = new string[visibleShortcuts.Count];
        for (int index = 0; index < visibleShortcuts.Count; index++)
        {
            hintSegments[index] = visibleShortcuts[index].ToHintSegment();
        }

        viewModel.HintText = string.Join("  |  ", hintSegments);
    }

    protected virtual bool ShouldOfferLeaveFieldShortcut()
    {
        return HasEditableTextInputFocus();
    }

    protected virtual void LeaveEditableField()
    {
        bool didFocus = Focus(NavigationMethod.Tab, KeyModifiers.None);
        _ = didFocus;
    }

    private static bool MatchesSlashShortcut(KeyEventArgs eventArgs)
    {
        if (eventArgs.KeyModifiers != KeyModifiers.None)
        {
            return false;
        }

        return eventArgs.Key == Key.Oem2
            || eventArgs.Key == Key.OemQuestion
            || eventArgs.Key == Key.Divide;
    }

    private ScreenShortcut? FindMatchingShortcut(KeyEventArgs eventArgs)
    {
        bool hasEditableTextInputFocus = HasEditableTextInputFocus();
        IReadOnlyList<ScreenShortcut> shortcuts = GetVisibleShortcuts(hasEditableTextInputFocus);
        foreach (ScreenShortcut shortcut in shortcuts)
        {
            if (shortcut.Matches(eventArgs, hasEditableTextInputFocus))
            {
                return shortcut;
            }
        }

        return null;
    }

    private IReadOnlyList<ScreenShortcut> GetVisibleShortcuts()
    {
        bool hasEditableTextInputFocus = HasEditableTextInputFocus();
        return GetVisibleShortcuts(hasEditableTextInputFocus);
    }

    private IReadOnlyList<ScreenShortcut> GetVisibleShortcuts(bool hasEditableTextInputFocus)
    {
        List<ScreenShortcut> visibleShortcuts = new();

        if (ShouldOfferLeaveFieldShortcut())
        {
            visibleShortcuts.Add(
                CreateShortcut(
                    Key.Escape,
                    KeyModifiers.None,
                    "Esc",
                    "Leave Field",
                    LeaveEditableField,
                    allowWhenTextInputFocused: true));
        }

        IReadOnlyList<ScreenShortcut> screenShortcuts = BuildScreenShortcuts();
        foreach (ScreenShortcut shortcut in screenShortcuts)
        {
            if (shortcut.IsVisible(hasEditableTextInputFocus))
            {
                visibleShortcuts.Add(shortcut);
            }
        }

        return visibleShortcuts;
    }

    private void AttachObservedViewModel(TViewModel? viewModel)
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

    private void OnObservedViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        RefreshShortcutHint();
    }

    private void OnScreenAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        AttachObservedViewModel(ViewModel);
        RefreshShortcutHint();
    }

    private void OnScreenDataContextChanged(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        AttachObservedViewModel(ViewModel);
        RefreshShortcutHint();
    }

    private void OnScreenDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        AttachObservedViewModel(null);
    }

    private void OnScreenGotFocus(object? sender, GotFocusEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        RefreshShortcutHint();
    }

    private void OnScreenLostFocus(object? sender, RoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        RefreshShortcutHint();
    }
}
