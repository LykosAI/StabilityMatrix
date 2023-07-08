using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentAvalonia.UI.Controls;

namespace StabilityMatrix.Avalonia.ViewModels;

public partial class SettingsViewModel : PageViewModelBase
{
    public override string Title => "Settings";
    public override Symbol Icon => Symbol.Setting;

    [ObservableProperty]
    private string? selectedTheme;
    
    public ObservableCollection<string> AvailableThemes { get; } = new()
    {
        "Light",
        "Dark",
        "System",
    };
    
    public override bool CanNavigateNext { get; protected set; }
    public override bool CanNavigatePrevious { get; protected set; }
}
