using System;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.ViewModels.Inference;
using StabilityMatrix.Core.Extensions;
#pragma warning disable CS0657 // Not a valid attribute location for this declaration

namespace StabilityMatrix.Avalonia.Controls;

public class StackEditableCard : TemplatedControl
{
    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        var listBox = e.NameScope.Find<ListBox>("PART_ListBox");
        if (listBox != null)
        {
            listBox.ContainerIndexChanged += (sender, args) =>
            {
                if (args.Container.DataContext is StackExpanderViewModel vm)
                {
                    vm.OnContainerIndexChanged(args.NewIndex);
                }
            };
        }

        var addButton = e.NameScope.Find<Button>("PART_AddButton");
        if (addButton != null)
        {
            addButton.Flyout = GetAddButtonFlyout();
        }
    }

    private string GetModuleDisplayName(Type moduleType)
    {
        var name = moduleType.Name;
        name = name.StripEnd("Module");

        // Add a space between lower and upper case letters, unless one part is 1 letter long
        /*name = Regex.Replace(name, @"(\P{Ll})(\P{Lu})", "$1 $2");*/

        return name;
    }

    private FAMenuFlyout GetAddButtonFlyout()
    {
        var vm = (DataContext as StackEditableCardViewModel)!;
        var flyout = new FAMenuFlyout();

        foreach (var moduleType in vm.AvailableModules)
        {
            var menuItem = new MenuFlyoutItem
            {
                Text = GetModuleDisplayName(moduleType),
                Command = vm.AddModuleCommand,
                CommandParameter = moduleType,
            };
            flyout.Items.Add(menuItem);
        }

        return flyout;
    }
}
