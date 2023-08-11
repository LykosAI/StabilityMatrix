using System;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Avalonia.ViewModels.PackageManager;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Factory;
using StabilityMatrix.Core.Services;
using Symbol = FluentIcons.Common.Symbol;
using SymbolIconSource = FluentIcons.FluentAvalonia.SymbolIconSource;

namespace StabilityMatrix.Avalonia.ViewModels;

/// <summary>
///  This is our ViewModel for the second page
/// </summary>

[View(typeof(PackageManagerPage))]
public partial class PackageManagerViewModel : PageViewModelBase
{
    private readonly ISettingsManager settingsManager;
    private readonly IPackageFactory packageFactory;
    private readonly ServiceManager<ViewModelBase> dialogFactory;

    public PackageManagerViewModel(ISettingsManager settingsManager, IPackageFactory packageFactory, 
        ServiceManager<ViewModelBase> dialogFactory)
    {
        this.settingsManager = settingsManager;
        this.packageFactory = packageFactory;
        this.dialogFactory = dialogFactory;
        
        EventManager.Instance.InstalledPackagesChanged += OnInstalledPackagesChanged;
    }

    [ObservableProperty] private ObservableCollection<PackageCardViewModel> packages;

    public override bool CanNavigateNext { get; protected set; } = true;
    public override bool CanNavigatePrevious { get; protected set; }
    public override string Title => "Packages";
    public override IconSource IconSource => new SymbolIconSource { Symbol = Symbol.Box, IsFilled = true};
    
    public override async Task OnLoadedAsync()
    {
        if (Design.IsDesignMode) return;
        
        var installedPackages = settingsManager.Settings.InstalledPackages;
        Packages = new ObservableCollection<PackageCardViewModel>(installedPackages.Select(
            package => dialogFactory.Get<PackageCardViewModel>(vm =>
            {
                vm.Package = package;
                return vm;
            })));
        
        foreach (var package in Packages)
        {
            await package.OnLoadedAsync();
        }
    }

    public async Task ShowInstallDialog()
    {
        var viewModel = dialogFactory.Get<InstallerViewModel>();
        viewModel.AvailablePackages = packageFactory.GetAllAvailablePackages().ToImmutableArray();
        viewModel.SelectedPackage = viewModel.AvailablePackages[0];

        var dialog = new BetterContentDialog
        {
            MaxDialogWidth = 1100,
            MinDialogWidth = 900,
            DefaultButton = ContentDialogButton.Close,
            IsPrimaryButtonEnabled = false,
            IsSecondaryButtonEnabled = false,
            IsFooterVisible = false,
            Content = new InstallerDialog
            {
                DataContext = viewModel
            }
        };

        await dialog.ShowAsync();
        await OnLoadedAsync();
    }

    private void OnInstalledPackagesChanged(object? sender, EventArgs e) =>OnLoaded();
}
