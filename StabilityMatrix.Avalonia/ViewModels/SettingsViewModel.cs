using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.ViewModels;

[View(typeof(SettingsPage))]
public partial class SettingsViewModel : PageViewModelBase
{
    public override string Title => "Settings";
    public override Symbol Icon => Symbol.Setting;

    [ObservableProperty]
    private string selectedTheme = "System";
    
    public ObservableCollection<string> AvailableThemes { get; } = new()
    {
        "Light",
        "Dark",
        "System",
    };

    [RelayCommand]
    private async Task DebugNotification()
    {

    }
    
    public override bool CanNavigateNext { get; protected set; }
    public override bool CanNavigatePrevious { get; protected set; }
}
