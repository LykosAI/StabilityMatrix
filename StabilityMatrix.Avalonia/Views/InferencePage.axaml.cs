using System;
using FluentAvalonia.UI.Controls;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.ViewModels;

namespace StabilityMatrix.Avalonia.Views;

[RegisterSingleton<InferencePage>]
public partial class InferencePage : UserControlBase
{
    public InferencePage()
    {
        InitializeComponent();
    }

    private void TabView_OnTabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        (DataContext as InferenceViewModel)?.OnTabCloseRequested(args);
    }

    private void TabView_OnAddTabButtonClick(TabView sender, EventArgs args)
    {
        (DataContext as InferenceViewModel)?.ShowAddTabDialogCommand.Execute(null);
    }
}
