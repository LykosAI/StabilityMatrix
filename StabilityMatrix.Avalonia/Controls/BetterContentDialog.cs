using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Styling;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;

namespace StabilityMatrix.Avalonia.Controls;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
public class BetterContentDialog : ContentDialog
{
    #region Reflection Shenanigans for setting content dialog result
    [NotNull]
    protected static readonly FieldInfo? ResultField = typeof(ContentDialog).GetField(
        "_result",BindingFlags.Instance | BindingFlags.NonPublic);
    
    protected ContentDialogResult Result
    {
        get => (ContentDialogResult) ResultField.GetValue(this)!;
        set => ResultField.SetValue(this, value);
    }
    
    [NotNull]
    protected static readonly MethodInfo? HideCoreMethod = typeof(ContentDialog).GetMethod(
        "HideCore", BindingFlags.Instance | BindingFlags.NonPublic);

    protected void HideCore()
    {
        HideCoreMethod.Invoke(this, null);
    }
    
    static BetterContentDialog()
    {
        if (ResultField is null) throw new NullReferenceException(
            "ResultField was not resolved");
        if (HideCoreMethod is null) throw new NullReferenceException(
            "HideCoreMethod was not resolved");
    }
    #endregion
    
    protected override Type StyleKeyOverride { get; } = typeof(ContentDialog);

    public static readonly StyledProperty<bool> IsFooterVisibleProperty = AvaloniaProperty.Register<BetterContentDialog, bool>(
        "IsFooterVisible", true);

    public bool IsFooterVisible
    {
        get => GetValue(IsFooterVisibleProperty);
        set => SetValue(IsFooterVisibleProperty, value);
    }

    public BetterContentDialog()
    {
        AddHandler(LoadedEvent, OnLoaded);
    }

    private void TryBindButtons()
    {
        if ((Content as Control)?.DataContext is not ContentDialogViewModelBase viewModel) return;

        viewModel.PrimaryButtonClick += OnDialogButtonClick;
        viewModel.SecondaryButtonClick += OnDialogButtonClick;
        viewModel.CloseButtonClick += OnDialogButtonClick;
    }

    protected void OnDialogButtonClick(object? sender, ContentDialogResult e)
    {
        Result = e;
        HideCore();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        
        TryBindButtons();
    }

    private void OnLoaded(object? sender, RoutedEventArgs? e)
    {
        TryBindButtons();
        // Check if we need to hide the footer
        if (IsFooterVisible) return;
        
        // Find the named grid
        // https://github.com/amwx/FluentAvalonia/blob/master/src/FluentAvalonia/Styling/
        // ControlThemes/FAControls/ContentDialogStyles.axaml#L96

        var border = VisualChildren[0] as Border;
        var panel = border?.Child as Panel;
        var faBorder = panel?.Children[0] as FABorder;
        var border2 = faBorder?.Child as Border;
        var grid = border2?.Child as Grid;

        // Get the parent border, which is what we want to hide
        if (grid?.Children[1] is not Border actualBorder)
        {
            throw new InvalidOperationException("Could not find parent border");
        }
        // Hide the border
        actualBorder.IsVisible = false;
    }
}
