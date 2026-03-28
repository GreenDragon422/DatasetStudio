using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;

namespace DatasetStudio.ViewModels;

public partial class ViewModelBase : ObservableRecipient
{
    protected ViewModelBase(IMessenger messenger)
        : base(messenger)
    {
    }

    [ObservableProperty]
    private string hintText = string.Empty;

    [ObservableProperty]
    private string statusText = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<HintBarItemViewModel> hintItems = Array.Empty<HintBarItemViewModel>();

    [ObservableProperty]
    private object? topBarContent;
}
