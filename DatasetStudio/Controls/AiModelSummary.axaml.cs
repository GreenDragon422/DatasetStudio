using Avalonia;
using Avalonia.Controls;
using DatasetStudio.Models;

namespace DatasetStudio.Controls;

public partial class AiModelSummary : UserControl
{
    public static readonly StyledProperty<AiModelSummaryState> SummaryProperty =
        AvaloniaProperty.Register<AiModelSummary, AiModelSummaryState>(nameof(Summary), AiModelSummaryState.NoneSelected());

    public AiModelSummary()
    {
        InitializeComponent();
    }

    public AiModelSummaryState Summary
    {
        get => GetValue(SummaryProperty);
        set => SetValue(SummaryProperty, value);
    }
}
