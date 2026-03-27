using Avalonia;
using Avalonia.Controls;

namespace DatasetStudio.Controls;

public partial class StatusBar : UserControl
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<StatusBar, string>(nameof(Text), string.Empty);

    public StatusBar()
    {
        InitializeComponent();
    }

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
}
