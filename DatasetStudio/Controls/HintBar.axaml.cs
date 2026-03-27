using Avalonia;
using Avalonia.Controls;

namespace DatasetStudio.Controls;

public partial class HintBar : UserControl
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<HintBar, string>(nameof(Text), string.Empty);

    public HintBar()
    {
        InitializeComponent();
    }

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
}
