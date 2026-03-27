using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace DatasetStudio.Controls;

public partial class TagPill : UserControl
{
    public new static readonly StyledProperty<string> TagProperty =
        AvaloniaProperty.Register<TagPill, string>(nameof(Tag), string.Empty);

    public static readonly StyledProperty<ICommand?> RemoveCommandProperty =
        AvaloniaProperty.Register<TagPill, ICommand?>(nameof(RemoveCommand));

    public TagPill()
    {
        InitializeComponent();
    }

    public new string Tag
    {
        get => GetValue(TagProperty);
        set => SetValue(TagProperty, value);
    }

    public ICommand? RemoveCommand
    {
        get => GetValue(RemoveCommandProperty);
        set => SetValue(RemoveCommandProperty, value);
    }
}
