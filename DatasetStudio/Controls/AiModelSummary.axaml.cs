using Avalonia;
using Avalonia.Controls;

namespace DatasetStudio.Controls;

public partial class AiModelSummary : UserControl
{
    public static readonly StyledProperty<string> ModelNameProperty =
        AvaloniaProperty.Register<AiModelSummary, string>(nameof(ModelName), "No AI model selected.");

    public static readonly StyledProperty<string> StatusTextProperty =
        AvaloniaProperty.Register<AiModelSummary, string>(nameof(StatusText), string.Empty);

    public static readonly StyledProperty<bool> HasStatusProperty =
        AvaloniaProperty.Register<AiModelSummary, bool>(nameof(HasStatus));

    public static readonly StyledProperty<bool> IsInstalledProperty =
        AvaloniaProperty.Register<AiModelSummary, bool>(nameof(IsInstalled));

    public static readonly StyledProperty<bool> IsBusyProperty =
        AvaloniaProperty.Register<AiModelSummary, bool>(nameof(IsBusy));

    public static readonly StyledProperty<bool> IsAttentionProperty =
        AvaloniaProperty.Register<AiModelSummary, bool>(nameof(IsAttention));

    public AiModelSummary()
    {
        InitializeComponent();
    }

    public string ModelName
    {
        get
        {
            return GetValue(ModelNameProperty);
        }
        set
        {
            SetValue(ModelNameProperty, value);
        }
    }

    public string StatusText
    {
        get
        {
            return GetValue(StatusTextProperty);
        }
        set
        {
            SetValue(StatusTextProperty, value);
        }
    }

    public bool HasStatus
    {
        get
        {
            return GetValue(HasStatusProperty);
        }
        set
        {
            SetValue(HasStatusProperty, value);
        }
    }

    public bool IsInstalled
    {
        get
        {
            return GetValue(IsInstalledProperty);
        }
        set
        {
            SetValue(IsInstalledProperty, value);
        }
    }

    public bool IsBusy
    {
        get
        {
            return GetValue(IsBusyProperty);
        }
        set
        {
            SetValue(IsBusyProperty, value);
        }
    }

    public bool IsAttention
    {
        get
        {
            return GetValue(IsAttentionProperty);
        }
        set
        {
            SetValue(IsAttentionProperty, value);
        }
    }
}
