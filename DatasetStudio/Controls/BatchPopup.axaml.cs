using System.Collections;
using Avalonia;
using Avalonia.Controls.Primitives;

namespace DatasetStudio.Controls;

public partial class BatchPopup : Popup
{
    public static readonly StyledProperty<BatchPopupMode> ModeProperty =
        AvaloniaProperty.Register<BatchPopup, BatchPopupMode>(nameof(Mode));

    public static readonly StyledProperty<string> QueryTextProperty =
        AvaloniaProperty.Register<BatchPopup, string>(nameof(QueryText), string.Empty);

    public static readonly StyledProperty<IEnumerable?> SuggestionsProperty =
        AvaloniaProperty.Register<BatchPopup, IEnumerable?>(nameof(Suggestions));

    public static readonly StyledProperty<object?> SelectedSuggestionProperty =
        AvaloniaProperty.Register<BatchPopup, object?>(nameof(SelectedSuggestion), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public BatchPopup()
    {
        InitializeComponent();
    }

    public BatchPopupMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public string QueryText
    {
        get => GetValue(QueryTextProperty);
        set => SetValue(QueryTextProperty, value);
    }

    public IEnumerable? Suggestions
    {
        get => GetValue(SuggestionsProperty);
        set => SetValue(SuggestionsProperty, value);
    }

    public object? SelectedSuggestion
    {
        get => GetValue(SelectedSuggestionProperty);
        set => SetValue(SelectedSuggestionProperty, value);
    }
}
