using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NLog;
using StabilityMatrix.Controls;
using StabilityMatrix.Core.Models.Progress;
using Wpf.Ui.Common;
using Wpf.Ui.Controls;
using Wpf.Ui.Controls.IconElements;

namespace StabilityMatrix.ViewModels;

public partial class RefreshBadgeViewModel : ObservableObject
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    
    public string WorkingToolTipText = "Loading...";
    public string SuccessToolTipText = "Success";
    public string InactiveToolTipText = "";
    public string FailToolTipText = "Failed";
    
    public SymbolIcon InactiveIcon = new(SymbolRegular.Circle12);
    public SymbolIcon SuccessIcon = new(SymbolRegular.CheckmarkCircle12, true);
    public SymbolIcon FailIcon = new(SymbolRegular.ErrorCircle12);
    
    public SolidColorBrush SuccessColorBrush = AppBrushes.SuccessGreen;
    public SolidColorBrush InactiveColorBrush = AppBrushes.WarningYellow;
    public SolidColorBrush FailColorBrush = AppBrushes.FailedRed;
    
    public Func<Task<bool>>? RefreshFunc { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWorking))]
    [NotifyPropertyChangedFor(nameof(Appearance))]
    [NotifyPropertyChangedFor(nameof(ColorBrush))]
    [NotifyPropertyChangedFor(nameof(CurrentToolTip))]
    [NotifyPropertyChangedFor(nameof(Icon))]
    private ProgressState state;
    
    public bool IsWorking => State == ProgressState.Working;

    public ControlAppearance Appearance => State switch
    {
        ProgressState.Working => ControlAppearance.Info,
        ProgressState.Success => ControlAppearance.Success,
        ProgressState.Failed => ControlAppearance.Danger,
        _ => ControlAppearance.Secondary,
    };
    
    public SolidColorBrush ColorBrush => State switch
    {
        ProgressState.Success => SuccessColorBrush,
        ProgressState.Inactive => InactiveColorBrush,
        ProgressState.Failed => FailColorBrush,
        _ => Brushes.Gray,
    };

    public string CurrentToolTip => State switch
    {
        ProgressState.Working => WorkingToolTipText,
        ProgressState.Success => SuccessToolTipText,
        ProgressState.Inactive => InactiveToolTipText,
        ProgressState.Failed => FailToolTipText,
        _ => ""
    };
    
    public SymbolIcon Icon => State switch
    {
        ProgressState.Success => SuccessIcon,
        ProgressState.Failed => FailIcon,
        _ => InactiveIcon
    };

    public RefreshBadgeViewModel()
    {
        Logger.Info("New RefreshVM instance!");
    }

    [RelayCommand]
    private async Task Refresh()
    {
        Logger.Info("Running refresh command...");
        if (RefreshFunc == null) return;
        
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
