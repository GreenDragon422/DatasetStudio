using Avalonia;
using Avalonia.Controls;
using DatasetStudio.Models;

namespace DatasetStudio.Controls;

public partial class StatusDot : UserControl
{
    public static readonly StyledProperty<TagStatus> StatusProperty =
        AvaloniaProperty.Register<StatusDot, TagStatus>(nameof(Status));

    public StatusDot()
    {
        InitializeComponent();
    }

    public TagStatus Status
    {
        get => GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }
}
