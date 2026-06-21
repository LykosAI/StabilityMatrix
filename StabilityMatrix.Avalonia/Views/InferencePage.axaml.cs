using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using FluentAvalonia.UI.Controls;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Extensions;
using StabilityMatrix.Avalonia.ViewModels;

namespace StabilityMatrix.Avalonia.Views;

[RegisterSingleton<InferencePage>]
public partial class InferencePage : UserControlBase
{
    private Button? addButton;

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
        if (DataContext is not InferenceViewModel vm)
            return;

        if (vm.UseClassicTabPicker)
        {
            ShowClassicAddTabFlyout(sender, vm);
        }
        else
        {
            vm.ShowAddTabDialogCommand.Execute(null);
        }
    }

    /// <summary>
    /// Shows the classic dropdown "add tab" menu, built from the same project-type metadata as the
    /// gallery picker so the two stay in sync.
    /// </summary>
    private void ShowClassicAddTabFlyout(TabView tabView, InferenceViewModel vm)
    {
        addButton ??= tabView
            .GetTemplateChildren()
            .OfType<Button>()
            .FirstOrDefault(b => b.Name == "AddButton");

        if (addButton is null)
            return;

        var flyout = new FAMenuFlyout();
        var isFirstGroup = true;

        foreach (var group in InferenceProjectTypeExtensions.GetGrouped())
        {
            if (!isFirstGroup)
            {
                flyout.Items.Add(new MenuFlyoutSeparator());
            }
            isFirstGroup = false;

            foreach (var info in group.Items)
            {
                flyout.Items.Add(
                    new MenuFlyoutItem
                    {
                        Text = info.DisplayName,
                        IconSource = new FluentIcons.Avalonia.Fluent.SymbolIconSource { Symbol = info.Icon },
                        Command = vm.AddTabCommand,
                        CommandParameter = info.Type,
                    }
                );
            }
        }

        flyout.ShowAt(addButton);
    }
}
