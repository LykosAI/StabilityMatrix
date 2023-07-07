using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Avalonia.ViewModels;

/// <summary>
///  This is our ViewModel for the second page
/// </summary>

[View(typeof(PackageManagerPage))]
public partial class PackageManagerViewModel : PageViewModelBase
{
    public PackageManagerViewModel()
    {
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressBarVisibility))]
    private int progressValue;
    
    [ObservableProperty]
    private InstalledPackage selectedPackage;
    
    [ObservableProperty]
    private string progressText;
    
    [ObservableProperty]
    private bool isIndeterminate;

    [ObservableProperty]
    private string installButtonText;

    [ObservableProperty] 
    private bool installButtonEnabled;

    [ObservableProperty] 
    private bool installButtonVisibility;

    [ObservableProperty] 
    private bool isUninstalling;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedPackage))]
    private bool updateAvailable;
    
    public bool ProgressBarVisibility => ProgressValue > 0 || IsIndeterminate;
    public ObservableCollection<InstalledPackage> Packages { get; }

    public override bool CanNavigateNext { get; protected set; } = true;
    public override bool CanNavigatePrevious { get; protected set; }
    public override string Title => "Packages";
    public override Symbol Icon => Symbol.XboxConsoleFilled;

    [RelayCommand]
    private async Task Install()
    {
        
    }

    [RelayCommand]
    private async Task Uninstall()
    {
        
    }

    [RelayCommand]
    private async Task ShowInstallWindow()
    {
        
    }
}
