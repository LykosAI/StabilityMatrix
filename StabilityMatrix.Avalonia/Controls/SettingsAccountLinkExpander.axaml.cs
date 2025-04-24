using System;
using System.Collections.Generic;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Metadata;
using Avalonia.VisualTree;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Core.Processes;

namespace StabilityMatrix.Avalonia.Controls;

public class SettingsAccountLinkExpander : TemplatedControlBase
{
    private readonly List<object?> _items = new();

    [Content]
    public List<object?> Items => _items;

    // ReSharper disable MemberCanBePrivate.Global
    public static readonly StyledProperty<object?> HeaderProperty =
        HeaderedItemsControl.HeaderProperty.AddOwner<SettingsAccountLinkExpander>();

    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public static readonly StyledProperty<Uri?> HeaderTargetUriProperty = AvaloniaProperty.Register<
        SettingsAccountLinkExpander,
        Uri?
    >("HeaderTargetUri");

    public Uri? HeaderTargetUri
    {
        get => GetValue(HeaderTargetUriProperty);
        set => SetValue(HeaderTargetUriProperty, value);
    }

    public static readonly StyledProperty<IconSource?> IconSourceProperty =
        SettingsExpander.IconSourceProperty.AddOwner<SettingsAccountLinkExpander>();

    public IconSource? IconSource
    {
        get => GetValue(IconSourceProperty);
        set => SetValue(IconSourceProperty, value);
    }

    public static readonly StyledProperty<bool> IsConnectedProperty = AvaloniaProperty.Register<
        SettingsAccountLinkExpander,
        bool
    >("IsConnected");

    public bool IsConnected
    {
        get => GetValue(IsConnectedProperty);
        set => SetValue(IsConnectedProperty, value);
    }

    public static readonly StyledProperty<object?> OnDescriptionProperty = AvaloniaProperty.Register<
        SettingsAccountLinkExpander,
        object?
    >("OnDescription", Languages.Resources.Label_Connected);

    public object? OnDescription
    {
        get => GetValue(OnDescriptionProperty);
        set => SetValue(OnDescriptionProperty, value);
    }

    public static readonly StyledProperty<object?> OnDescriptionExtraProperty = AvaloniaProperty.Register<
        SettingsAccountLinkExpander,
        object?
    >("OnDescriptionExtra");

    public object? OnDescriptionExtra
    {
        get => GetValue(OnDescriptionExtraProperty);
        set => SetValue(OnDescriptionExtraProperty, value);
    }

    public static readonly StyledProperty<object?> OffDescriptionProperty = AvaloniaProperty.Register<
        SettingsAccountLinkExpander,
        object?
    >("OffDescription");

    public object? OffDescription
    {
        get => GetValue(OffDescriptionProperty);
        set => SetValue(OffDescriptionProperty, value);
    }

    public static readonly StyledProperty<ICommand?> ConnectCommandProperty = AvaloniaProperty.Register<
        SettingsAccountLinkExpander,
        ICommand?
    >(nameof(ConnectCommand), enableDataValidation: true);

    public ICommand? ConnectCommand
    {
        get => GetValue(ConnectCommandProperty);
        set => SetValue(ConnectCommandProperty, value);
    }

    public static readonly StyledProperty<ICommand?> DisconnectCommandProperty = AvaloniaProperty.Register<
        SettingsAccountLinkExpander,
        ICommand?
    >(nameof(DisconnectCommand), enableDataValidation: true);

    public ICommand? DisconnectCommand
    {
        get => GetValue(DisconnectCommandProperty);
        set => SetValue(DisconnectCommandProperty, value);
    }

    /*public static readonly StyledProperty<bool> IsLoading2Property = AvaloniaProperty.Register<SettingsAccountLinkExpander, bool>(
        nameof(IsLoading2));

    public bool IsLoading2
    {
        get => GetValue(IsLoading2Property);
        set => SetValue(IsLoading2Property, value);
    }*/

    private bool _isLoading;

    public static readonly DirectProperty<SettingsAccountLinkExpander, bool> IsLoadingProperty =
        AvaloniaProperty.RegisterDirect<SettingsAccountLinkExpander, bool>(
            "IsLoading",
            o => o.IsLoading,
            (o, v) => o.IsLoading = v
        );

    public bool IsLoading
    {
        get => _isLoading;
        set => SetAndRaise(IsLoadingProperty, ref _isLoading, value);
    }

    // ReSharper restore MemberCanBePrivate.Global

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        // Bind tapped event on header
        if (
            HeaderTargetUri is { } headerTargetUri
            && e.NameScope.Find<TextBlock>("PART_HeaderTextBlock") is { } headerTextBlock
        )
        {
            headerTextBlock.Tapped += (_, _) =>
            {
                ProcessRunner.OpenUrl(headerTargetUri.ToString());
            };
        }

        if (e.NameScope.Find<SettingsExpander>("PART_SettingsExpander") is { } expander)
        {
            expander.ItemsSource = Items;
        }

        if (ConnectCommand is { } command)
        {
            var connectButton = e.NameScope.Get<Button>("PART_ConnectButton");
            connectButton.Command = command;
        }

        if (DisconnectCommand is { } disconnectCommand)
        {
            var disconnectMenuItem = e.NameScope.Get<MenuFlyoutItem>("PART_DisconnectMenuItem");
            disconnectMenuItem.Command = disconnectCommand;
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (!this.IsAttachedToVisualTree())
        {
            return;
        }

        if (change.Property == ConnectCommandProperty)
        {
            var button = this.GetControl<Button>("PART_ConnectButton");
            button.Command = ConnectCommand;
        }

        if (change.Property == DisconnectCommandProperty)
        {
            var button = this.GetControl<Button>("PART_DisconnectButton");
            button.Command = DisconnectCommand;
        }
    }
}
