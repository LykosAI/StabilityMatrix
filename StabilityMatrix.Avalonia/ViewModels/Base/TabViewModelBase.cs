using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentAvalonia.UI.Controls;

namespace StabilityMatrix.Avalonia.ViewModels.Base;

public abstract partial class TabViewModelBase : ViewModelBase
{
    [ObservableProperty]
    private List<ICommandBarElement> primaryCommands = new();

    public abstract string Header { get; }
}
