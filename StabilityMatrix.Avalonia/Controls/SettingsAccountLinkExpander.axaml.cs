using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using FluentAvalonia.UI.Controls;

namespace StabilityMatrix.Avalonia.Controls;

public class SettingsAccountLinkExpander : TemplatedControl
{
    // ReSharper disable MemberCanBePrivate.Global
    public static readonly StyledProperty<object?> HeaderProperty =
        HeaderedItemsControl.HeaderProperty.AddOwner<SettingsAccountLinkExpander>();

    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
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

    public static readonly StyledProperty<object?> OnDescriptionProperty =
        AvaloniaProperty.Register<SettingsAccountLinkExpander, object?>(
            "OnDescription",
            Languages.Resources.Label_Connected
        );

    public object? OnDescription
    {
        get => GetValue(OnDescriptionProperty);
        set => SetValue(OnDescriptionProperty, value);
    }

    public static readonly StyledProperty<object?> OffDescriptionProperty =
        AvaloniaProperty.Register<SettingsAccountLinkExpander, object?>("OffDescription");

    public object? OffDescription
    {
        get => GetValue(OffDescriptionProperty);
        set => SetValue(OffDescriptionProperty, value);
    }

    public static readonly StyledProperty<ICommand?> ConnectCommandProperty =
        AvaloniaProperty.Register<SettingsAccountLinkExpander, ICommand?>(
            nameof(ConnectCommand),
            enableDataValidation: true
        );

    public ICommand? ConnectCommand
    {
        get => GetValue(ConnectCommandProperty);
        set => SetValue(ConnectCommandProperty, value);
    }

    public static readonly StyledProperty<ICommand?> DisconnectCommandProperty =
        AvaloniaProperty.Register<SettingsAccountLinkExpander, ICommand?>(
            nameof(DisconnectCommand),
            enableDataValidation: true
        );

    public ICommand? DisconnectCommand
    {
        get => GetValue(DisconnectCommandProperty);
        set => SetValue(DisconnectCommandProperty, value);
    }

    // ReSharper restore MemberCanBePrivate.Global

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

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
