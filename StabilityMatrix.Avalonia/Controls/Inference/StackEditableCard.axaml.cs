using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.ViewModels.Inference;
using Injectio.Attributes;
using StabilityMatrix.Core.Extensions;
#pragma warning disable CS0657 // Not a valid attribute location for this declaration

namespace StabilityMatrix.Avalonia.Controls;

[PseudoClasses(":editEnabled")]
[RegisterTransient<StackEditableCard>]
public class StackEditableCard : TemplatedControl
{
    private ListBox? listBoxPart;

    // ReSharper disable once MemberCanBePrivate.Global
    public static readonly StyledProperty<bool> IsListBoxEditEnabledProperty = AvaloniaProperty.Register<
        StackEditableCard,
        bool
    >("IsListBoxEditEnabled");

    public bool IsListBoxEditEnabled
    {
        get => GetValue(IsListBoxEditEnabledProperty);
        set => SetValue(IsListBoxEditEnabledProperty, value);
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        listBoxPart = e.NameScope.Find<ListBox>("PART_ListBox");
        if (listBoxPart != null)
        {
            // Register handlers to attach container behavior

            // Forward container index changes to view model
            ((IChildIndexProvider)listBoxPart).ChildIndexChanged += (_, args) =>
            {
                if (args.Child is Control { DataContext: StackExpanderViewModel vm })
                {
                    vm.OnContainerIndexChanged(args.Index);
                }
            };
        }

        if (e.NameScope.Find<Button>("PART_AddButton") is { } addButton)
        {
            addButton.Flyout = GetAddButtonFlyout();
        }
    }

    /// <inheritdoc />
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        UpdatePseudoClasses(IsListBoxEditEnabled);
    }

    private void UpdatePseudoClasses(bool editEnabled)
    {
        PseudoClasses.Set(":editEnabled", IsListBoxEditEnabled);

        listBoxPart?.Classes.Set("draggableVirtualizing", IsListBoxEditEnabled);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsListBoxEditEnabledProperty)
        {
            UpdatePseudoClasses(change.GetNewValue<bool>());
        }
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

    private static string GetModuleDisplayName(Type moduleType)
    {
        var name = moduleType.Name;
        name = name.StripEnd("Module");

        // Add a space between lower and upper case letters, unless one part is 1 letter long
        /*name = Regex.Replace(name, @"(\P{Ll})(\P{Lu})", "$1 $2");*/

        return name;
    }
}
