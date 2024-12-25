using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using FluentAvalonia.UI.Controls;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.ViewModels;
using StabilityMatrix.Core.Models.Inference;

namespace StabilityMatrix.Avalonia.Views;

[RegisterSingleton<InferencePage>]
public partial class InferencePage : UserControlBase
{
    private Button? _addButton;
    private Button AddButton =>
        _addButton ??= this.FindControl<TabView>("TabView")!
            .GetTemplateChildren()
            .OfType<Button>()
            .First(p => p.Name == "AddButton");

    private readonly CommandBarFlyout addTabFlyout;

    public InferencePage()
    {
        InitializeComponent();

        addTabFlyout =
            Resources["AddTabFlyout"] as CommandBarFlyout
            ?? throw new NullReferenceException("AddTabFlyout not found");
    }

    private void TabView_OnTabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        (DataContext as InferenceViewModel)?.OnTabCloseRequested(args);
    }

    private void TabView_OnAddTabButtonClick(TabView sender, EventArgs args)
    {
        ShowAddTabMenu(false);
    }

    private void ShowAddTabMenu(bool isTransient)
    {
        addTabFlyout.ShowMode = isTransient ? FlyoutShowMode.Transient : FlyoutShowMode.Standard;

        addTabFlyout.ShowAt(AddButton);
    }

    private void AddTabMenu_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: InferenceProjectType type })
        {
            (DataContext as InferenceViewModel)!.AddTabCommand.Execute(type);
        }
    }

    private void AddTabMenu_ImgToVideo_OnClick(object? sender, RoutedEventArgs e)
    {
        (DataContext as InferenceViewModel)!.AddTabCommand.Execute(InferenceProjectType.ImageToVideo);
    }
}
