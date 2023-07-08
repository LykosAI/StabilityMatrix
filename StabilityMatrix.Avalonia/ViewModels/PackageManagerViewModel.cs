using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper.Factory;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels;

/// <summary>
///  This is our ViewModel for the second page
/// </summary>

[View(typeof(PackageManagerPage))]
public partial class PackageManagerViewModel : PageViewModelBase
{
    private readonly ISettingsManager settingsManager;
    private readonly IPackageFactory packageFactory;

    private const int MinutesToWaitForUpdateCheck = 60;

    public PackageManagerViewModel(ISettingsManager settingsManager, IPackageFactory packageFactory)
    {
        this.settingsManager = settingsManager;
        this.packageFactory = packageFactory;

        ProgressText = string.Empty;
        InstallButtonText = "Install";
        InstallButtonEnabled = true;
        ProgressValue = 0;
        IsIndeterminate = false;
        Packages = new ObservableCollection<InstalledPackage>(settingsManager.Settings.InstalledPackages);

        if (Packages.Any())
        {
            SelectedPackage = Packages[0];
            InstallButtonVisibility = true;
        }
        else
        {
            SelectedPackage = new InstalledPackage
            {
                DisplayName = "Click \"Add Package\" to install a package"
            };
        }
    }
    
    [ObservableProperty, NotifyPropertyChangedFor(nameof(ProgressBarVisibility))]
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

    public override async Task OnLoadedAsync()
    {
        Packages.Clear();
        var installedPackages = settingsManager.Settings.InstalledPackages;
        if (installedPackages.Count == 0)
        {
            SelectedPackage = new InstalledPackage
            {
                DisplayName = "Click \"Add Package\" to install a package"
            };
            InstallButtonVisibility = false;
            
            return;
        }
        
        
        foreach (var packageToUpdate in installedPackages)
        {
            var basePackage = packageFactory.FindPackageByName(packageToUpdate.PackageName);
            if (basePackage == null) continue;
            
            var canCheckUpdate = packageToUpdate.LastUpdateCheck == null ||
                                 packageToUpdate.LastUpdateCheck.Value.AddMinutes(MinutesToWaitForUpdateCheck) <
                                 DateTimeOffset.Now;
            if (canCheckUpdate)
            {
                var hasUpdate = await basePackage.CheckForUpdates(packageToUpdate.DisplayName);
                packageToUpdate.UpdateAvailable = hasUpdate;
                packageToUpdate.LastUpdateCheck = DateTimeOffset.Now;
                settingsManager.SetLastUpdateCheck(packageToUpdate);
            }

            Packages.Add(packageToUpdate);
        }

        SelectedPackage =
            installedPackages.FirstOrDefault(x => x.Id == settingsManager.Settings.ActiveInstalledPackage) ??
            Packages[0];
    }
    
    partial void OnSelectedPackageChanged(InstalledPackage? value)
    {
        if (value == null) return;
        
        UpdateAvailable = value.UpdateAvailable;
        InstallButtonText = value.UpdateAvailable ? "Update" : "Launch";
        InstallButtonVisibility = true;
    }

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
