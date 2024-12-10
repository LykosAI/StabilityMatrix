using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using NLog;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Styles;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using Injectio.Attributes;
using StabilityMatrix.Core.Models.Progress;

namespace StabilityMatrix.Avalonia.ViewModels;

[View(typeof(RefreshBadge))]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[ManagedService]
[RegisterTransient<RefreshBadgeViewModel>]
public partial class RefreshBadgeViewModel : ViewModelBase
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public string WorkingToolTipText { get; set; } = "Loading...";
    public string SuccessToolTipText { get; set; } = "Success";
    public string InactiveToolTipText { get; set; } = "";
    public string FailToolTipText { get; set; } = "Failed";

    public Symbol InactiveIcon { get; set; } = Symbol.Clear;
    public Symbol SuccessIcon { get; set; } = Symbol.Checkmark;
    public Symbol FailIcon { get; set; } = Symbol.AlertUrgent;

    public IBrush SuccessColorBrush { get; set; } = ThemeColors.ThemeGreen;
    public IBrush InactiveColorBrush { get; set; } = ThemeColors.ThemeYellow;
    public IBrush FailColorBrush { get; set; } = ThemeColors.ThemeYellow;

    public Func<Task<bool>>? RefreshFunc { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWorking))]
    [NotifyPropertyChangedFor(nameof(ColorBrush))]
    [NotifyPropertyChangedFor(nameof(CurrentToolTip))]
    [NotifyPropertyChangedFor(nameof(Icon))]
    private ProgressState state;

    public bool IsWorking => State == ProgressState.Working;

    /*public ControlAppearance Appearance => State switch
    {
        ProgressState.Working => ControlAppearance.Info,
        ProgressState.Success => ControlAppearance.Success,
        ProgressState.Failed => ControlAppearance.Danger,
        _ => ControlAppearance.Secondary
    };*/

    public IBrush ColorBrush =>
        State switch
        {
            ProgressState.Success => SuccessColorBrush,
            ProgressState.Inactive => InactiveColorBrush,
            ProgressState.Failed => FailColorBrush,
            _ => Brushes.Gray
        };

    public string CurrentToolTip =>
        State switch
        {
            ProgressState.Working => WorkingToolTipText,
            ProgressState.Success => SuccessToolTipText,
            ProgressState.Inactive => InactiveToolTipText,
            ProgressState.Failed => FailToolTipText,
            _ => ""
        };

    public Symbol Icon =>
        State switch
        {
            ProgressState.Success => SuccessIcon,
            ProgressState.Failed => FailIcon,
            _ => InactiveIcon
        };

    [RelayCommand]
    private async Task Refresh()
    {
        Logger.Info("Running refresh command...");
        if (RefreshFunc == null)
            return;

        State = ProgressState.Working;
        try
        {
            var result = await RefreshFunc.Invoke();
            State = result ? ProgressState.Success : ProgressState.Failed;
        }
        catch (Exception ex)
        {
            State = ProgressState.Failed;
            Logger.Error(ex, "Refresh command failed: {Ex}", ex.Message);
        }
    }
}
