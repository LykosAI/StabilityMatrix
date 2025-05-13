using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using AsyncAwaitBestPractices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Logging;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Extensions;

namespace StabilityMatrix.Avalonia.Controls;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
public class BetterContentDialog : ContentDialog
{
    #region Reflection Shenanigans for setting content dialog result
    [NotNull]
    protected static readonly FieldInfo? ResultField = typeof(ContentDialog).GetField(
        "_result",
        BindingFlags.Instance | BindingFlags.NonPublic
    );

    protected ContentDialogResult Result
    {
        get => (ContentDialogResult)ResultField.GetValue(this)!;
        set => ResultField.SetValue(this, value);
    }

    [NotNull]
    protected static readonly MethodInfo? HideCoreMethod = typeof(ContentDialog).GetMethod(
        "HideCore",
        BindingFlags.Instance | BindingFlags.NonPublic
    );

    protected void HideCore()
    {
        HideCoreMethod.Invoke(this, null);
    }

    // Also get button properties to hide on command execution change
    [NotNull]
    protected static readonly FieldInfo? PrimaryButtonField = typeof(ContentDialog).GetField(
        "_primaryButton",
        BindingFlags.Instance | BindingFlags.NonPublic
    );

    protected Button? PrimaryButton
    {
        get => (Button?)PrimaryButtonField.GetValue(this)!;
        set => PrimaryButtonField.SetValue(this, value);
    }

    [NotNull]
    protected static readonly FieldInfo? SecondaryButtonField = typeof(ContentDialog).GetField(
        "_secondaryButton",
        BindingFlags.Instance | BindingFlags.NonPublic
    );

    protected Button? SecondaryButton
    {
        get => (Button?)SecondaryButtonField.GetValue(this)!;
        set => SecondaryButtonField.SetValue(this, value);
    }

    [NotNull]
    protected static readonly FieldInfo? CloseButtonField = typeof(ContentDialog).GetField(
        "_closeButton",
        BindingFlags.Instance | BindingFlags.NonPublic
    );

    protected Button? CloseButton
    {
        get => (Button?)CloseButtonField.GetValue(this)!;
        set => CloseButtonField.SetValue(this, value);
    }

    static BetterContentDialog()
    {
        if (ResultField is null)
        {
            throw new NullReferenceException("ResultField was not resolved");
        }
        if (HideCoreMethod is null)
        {
            throw new NullReferenceException("HideCoreMethod was not resolved");
        }
        if (PrimaryButtonField is null || SecondaryButtonField is null || CloseButtonField is null)
        {
            throw new NullReferenceException("Button fields were not resolved");
        }
    }
    #endregion

    private Border? backgroundPart;

    protected override Type StyleKeyOverride { get; } = typeof(ContentDialog);

    public static readonly StyledProperty<bool> IsFooterVisibleProperty = AvaloniaProperty.Register<
        BetterContentDialog,
        bool
    >("IsFooterVisible", true);

    public bool IsFooterVisible
    {
        get => GetValue(IsFooterVisibleProperty);
        set => SetValue(IsFooterVisibleProperty, value);
    }

    public static readonly StyledProperty<ScrollBarVisibility> ContentVerticalScrollBarVisibilityProperty =
        AvaloniaProperty.Register<BetterContentDialog, ScrollBarVisibility>(
            "ContentScrollBarVisibility",
            ScrollBarVisibility.Auto
        );

    public ScrollBarVisibility ContentVerticalScrollBarVisibility
    {
        get => GetValue(ContentVerticalScrollBarVisibilityProperty);
        set => SetValue(ContentVerticalScrollBarVisibilityProperty, value);
    }

    public static readonly StyledProperty<double> MinDialogWidthProperty = AvaloniaProperty.Register<
        BetterContentDialog,
        double
    >("MinDialogWidth");

    public double MinDialogWidth
    {
        get => GetValue(MinDialogWidthProperty);
        set => SetValue(MinDialogWidthProperty, value);
    }

    public static readonly StyledProperty<double> MaxDialogWidthProperty = AvaloniaProperty.Register<
        BetterContentDialog,
        double
    >("MaxDialogWidth");

    public double MaxDialogWidth
    {
        get => GetValue(MaxDialogWidthProperty);
        set => SetValue(MaxDialogWidthProperty, value);
    }

    public static readonly StyledProperty<double> MinDialogHeightProperty = AvaloniaProperty.Register<
        BetterContentDialog,
        double
    >("MinDialogHeight");

    public double MinDialogHeight
    {
        get => GetValue(MaxDialogHeightProperty);
        set => SetValue(MaxDialogHeightProperty, value);
    }

    public static readonly StyledProperty<double> MaxDialogHeightProperty = AvaloniaProperty.Register<
        BetterContentDialog,
        double
    >("MaxDialogHeight");

    public double MaxDialogHeight
    {
        get => GetValue(MaxDialogHeightProperty);
        set => SetValue(MaxDialogHeightProperty, value);
    }

    public static readonly StyledProperty<Thickness> ContentMarginProperty = AvaloniaProperty.Register<
        BetterContentDialog,
        Thickness
    >("ContentMargin");

    public Thickness ContentMargin
    {
        get => GetValue(ContentMarginProperty);
        set => SetValue(ContentMarginProperty, value);
    }

    public static readonly StyledProperty<bool> CloseOnClickOutsideProperty = AvaloniaProperty.Register<
        BetterContentDialog,
        bool
    >("CloseOnClickOutside");

    /// <summary>
    /// Whether to close the dialog when clicking outside of it (on the blurred background)
    /// </summary>
    public bool CloseOnClickOutside
    {
        get => GetValue(CloseOnClickOutsideProperty);
        set => SetValue(CloseOnClickOutsideProperty, value);
    }

    public BetterContentDialog()
    {
        AddHandler(LoadedEvent, OnLoaded);
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (CloseOnClickOutside)
        {
            if (e.Source is Popup || backgroundPart is null)
                return;

            var point = e.GetPosition(this);

            if (!backgroundPart.Bounds.Contains(point))
            {
                // Use vm if available
                if ((Content as Control)?.DataContext is ContentDialogViewModelBase vm)
                {
                    vm.OnCloseButtonClick();
                }
                else
                {
                    Hide(ContentDialogResult.None);
                }
            }
        }
    }

    private void TrySetButtonCommands()
    {
        // If commands provided, bind OnCanExecuteChanged to hide buttons
        // otherwise link visibility to IsEnabled
        if (PrimaryButton is not null)
        {
            if (PrimaryButtonCommand is not null)
            {
                PrimaryButtonCommand.CanExecuteChanged += (_, _) =>
                    PrimaryButton.IsEnabled = PrimaryButtonCommand.CanExecute(null);
                // Also set initial state
                PrimaryButton.IsEnabled = PrimaryButtonCommand.CanExecute(null);
            }
            else
            {
                PrimaryButton.IsVisible = IsPrimaryButtonEnabled && !string.IsNullOrEmpty(PrimaryButtonText);
            }
        }

        if (SecondaryButton is not null)
        {
            if (SecondaryButtonCommand is not null)
            {
                SecondaryButtonCommand.CanExecuteChanged += (_, _) =>
                    SecondaryButton.IsEnabled = SecondaryButtonCommand.CanExecute(null);
                // Also set initial state
                SecondaryButton.IsEnabled = SecondaryButtonCommand.CanExecute(null);
            }
            else
            {
                SecondaryButton.IsVisible =
                    IsSecondaryButtonEnabled && !string.IsNullOrEmpty(SecondaryButtonText);
            }
        }

        if (CloseButton is not null)
        {
            if (CloseButtonCommand is not null)
            {
                CloseButtonCommand.CanExecuteChanged += (_, _) =>
                    CloseButton.IsEnabled = CloseButtonCommand.CanExecute(null);
                // Also set initial state
                CloseButton.IsEnabled = CloseButtonCommand.CanExecute(null);
            }
        }
    }

    private void TryBindButtonEvents()
    {
        if ((Content as Control)?.DataContext is ContentDialogViewModelBase viewModel)
        {
            viewModel.PrimaryButtonClick += OnDialogButtonClick;
            viewModel.SecondaryButtonClick += OnDialogButtonClick;
            viewModel.CloseButtonClick += OnDialogButtonClick;
        }
        else if (Content is ContentDialogViewModelBase viewModelDirect)
        {
            viewModelDirect.PrimaryButtonClick += OnDialogButtonClick;
            viewModelDirect.SecondaryButtonClick += OnDialogButtonClick;
            viewModelDirect.CloseButtonClick += OnDialogButtonClick;
        }
        else if ((Content as Control)?.DataContext is ContentDialogProgressViewModelBase progressViewModel)
        {
            progressViewModel.PrimaryButtonClick += OnDialogButtonClick;
            progressViewModel.SecondaryButtonClick += OnDialogButtonClick;
            progressViewModel.CloseButtonClick += OnDialogButtonClick;
        }
    }

    protected void OnDialogButtonClick(object? sender, ContentDialogResult e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            Result = e;
            HideCore();
        });
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        TryBindButtonEvents();
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        backgroundPart = e.NameScope.Find<Border>("BackgroundElement");
        if (backgroundPart is not null)
        {
            backgroundPart.Margin = ContentMargin;
        }
    }

    private void OnLoaded(object? sender, RoutedEventArgs? e)
    {
        TryBindButtonEvents();

        try
        {
            // Find the named grid
            // https://github.com/amwx/FluentAvalonia/blob/master/src/FluentAvalonia/Styling/
            // ControlThemes/FAControls/ContentDialogStyles.axaml#L96
            var containerBorder = VisualChildren[0] as Border;
            var layoutRootPanel = containerBorder?.Child as Panel;
            var backgroundElementBorder = (layoutRootPanel?.Children[0] as Border).Unwrap();

            // Set dialog bounds
            if (MaxDialogWidth > 0)
            {
                backgroundElementBorder.MaxWidth = MaxDialogWidth;
            }

            if (MinDialogWidth > 0)
            {
                backgroundElementBorder.MinWidth = MinDialogWidth;
            }

            // This kind of bork for some reason
            /*if (MinDialogHeight > 0)
            {
                faBorder!.MinHeight = MinDialogHeight;
            }*/

            if (MaxDialogHeight > 0)
            {
                backgroundElementBorder!.MaxHeight = MaxDialogHeight;
            }

            var border2 = backgroundElementBorder?.Child as Border;
            // Named Grid 'DialogSpace'
            var dialogSpaceGrid = (border2?.Child as Grid).Unwrap();

            // Get the parent border, which is what we want to hide
            var scrollViewer = (dialogSpaceGrid.Children[0] as ScrollViewer).Unwrap();
            var actualBorder = (dialogSpaceGrid.Children[1] as Border).Unwrap();

            var subBorder = (scrollViewer.Content as Border).Unwrap();
            var subGrid = (subBorder.Child as Grid).Unwrap();

            var contentControlTitle = (subGrid.Children[0] as ContentControl).Unwrap();

            // Hide title if empty
            if (Title is null or string { Length: 0 })
            {
                contentControlTitle.IsVisible = false;
            }

            // Set footer and scrollbar visibility states
            actualBorder.IsVisible = IsFooterVisible;
            scrollViewer.VerticalScrollBarVisibility = ContentVerticalScrollBarVisibility;
        }
        catch (ArgumentNullException)
        {
            Logger
                .TryGet(LogEventLevel.Error, nameof(BetterContentDialog))
                ?.Log(this, "OnLoaded - Unable to find elements");

            return;
        }

        // Also call the vm's OnLoad
        // (UserControlBase handles this now, so we don't need to)
        /*if (Content is Control { DataContext: ViewModelBase viewModel })
        {
            viewModel.OnLoaded();
            Dispatcher.UIThread.InvokeAsync(viewModel.OnLoadedAsync).SafeFireAndForget();
        }*/
    }
}
