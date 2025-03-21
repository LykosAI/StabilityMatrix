using System.Diagnostics.CodeAnalysis;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.Primitives;
using CommunityToolkit.Mvvm.Input;

namespace StabilityMatrix.Avalonia.Controls;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public class Paginator : TemplatedControlBase
{
    private bool isFirstTemplateApplied;
    private ICommand? firstPageCommandBinding;
    private ICommand? previousPageCommandBinding;
    private ICommand? nextPageCommandBinding;
    private ICommand? lastPageCommandBinding;

    public static readonly StyledProperty<int> CurrentPageNumberProperty = AvaloniaProperty.Register<
        Paginator,
        int
    >("CurrentPageNumber", 1);

    public int CurrentPageNumber
    {
        get => GetValue(CurrentPageNumberProperty);
        set => SetValue(CurrentPageNumberProperty, value);
    }

    public static readonly StyledProperty<int> TotalPagesProperty = AvaloniaProperty.Register<Paginator, int>(
        "TotalPages",
        1
    );

    public int TotalPages
    {
        get => GetValue(TotalPagesProperty);
        set => SetValue(TotalPagesProperty, value);
    }

    public static readonly StyledProperty<ICommand?> FirstPageCommandProperty = AvaloniaProperty.Register<
        Paginator,
        ICommand?
    >("FirstPageCommand");

    public ICommand? FirstPageCommand
    {
        get => GetValue(FirstPageCommandProperty);
        set => SetValue(FirstPageCommandProperty, value);
    }

    public static readonly StyledProperty<ICommand?> PreviousPageCommandProperty = AvaloniaProperty.Register<
        Paginator,
        ICommand?
    >("PreviousPageCommand");

    public ICommand? PreviousPageCommand
    {
        get => GetValue(PreviousPageCommandProperty);
        set => SetValue(PreviousPageCommandProperty, value);
    }

    public static readonly StyledProperty<ICommand?> NextPageCommandProperty = AvaloniaProperty.Register<
        Paginator,
        ICommand?
    >("NextPageCommand");

    public ICommand? NextPageCommand
    {
        get => GetValue(NextPageCommandProperty);
        set => SetValue(NextPageCommandProperty, value);
    }

    public static readonly StyledProperty<ICommand?> LastPageCommandProperty = AvaloniaProperty.Register<
        Paginator,
        ICommand?
    >("LastPageCommand");

    public ICommand? LastPageCommand
    {
        get => GetValue(LastPageCommandProperty);
        set => SetValue(LastPageCommandProperty, value);
    }

    public static readonly StyledProperty<bool> CanNavForwardProperty = AvaloniaProperty.Register<
        Paginator,
        bool
    >("CanNavForward");

    public bool CanNavForward
    {
        get => GetValue(CanNavForwardProperty);
        set => SetValue(CanNavForwardProperty, value);
    }

    public static readonly StyledProperty<bool> CanNavBackProperty = AvaloniaProperty.Register<
        Paginator,
        bool
    >("CanNavBack");

    public bool CanNavBack
    {
        get => GetValue(CanNavBackProperty);
        set => SetValue(CanNavBackProperty, value);
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (!isFirstTemplateApplied)
        {
            firstPageCommandBinding = FirstPageCommand;
            previousPageCommandBinding = PreviousPageCommand;
            nextPageCommandBinding = NextPageCommand;
            lastPageCommandBinding = LastPageCommand;
            isFirstTemplateApplied = true;
        }

        // Wrap the commands
        FirstPageCommand = new RelayCommand(() =>
        {
            if (CurrentPageNumber > 1)
            {
                CurrentPageNumber = 1;
            }
            firstPageCommandBinding?.Execute(null);
        });

        PreviousPageCommand = new RelayCommand(() =>
        {
            if (CurrentPageNumber > 1)
            {
                CurrentPageNumber--;
            }
            previousPageCommandBinding?.Execute(null);
        });

        NextPageCommand = new RelayCommand(() =>
        {
            if (CurrentPageNumber < TotalPages)
            {
                CurrentPageNumber++;
            }
            nextPageCommandBinding?.Execute(null);
        });

        LastPageCommand = new RelayCommand(() =>
        {
            if (CurrentPageNumber < TotalPages)
            {
                CurrentPageNumber = TotalPages;
            }
            lastPageCommandBinding?.Execute(null);
        });
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // Update the CanNavForward and CanNavBack properties
        if (change.Property == CurrentPageNumberProperty && change.NewValue is int)
        {
            CanNavForward = (int)change.NewValue < TotalPages;
            CanNavBack = (int)change.NewValue > 1;
        }
        else if (change.Property == TotalPagesProperty && change.NewValue is int)
        {
            CanNavForward = CurrentPageNumber < (int)change.NewValue;
            CanNavBack = CurrentPageNumber > 1;
        }
    }
}
