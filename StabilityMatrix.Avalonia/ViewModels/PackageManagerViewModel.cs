using System;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using Microsoft.Extensions.Logging;
using Polly;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Avalonia.ViewModels.PackageManager;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Factory;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Models.Progress;
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
    private readonly ILogger<PackageManagerViewModel> logger;
    private readonly ISettingsManager settingsManager;
    private readonly IPackageFactory packageFactory;
    private readonly INotificationService notificationService;
    private readonly ServiceManager<ViewModelBase> dialogFactory;

    private const int MinutesToWaitForUpdateCheck = 60;

    public PackageManagerViewModel(ILogger<PackageManagerViewModel> logger,
        ISettingsManager settingsManager, IPackageFactory packageFactory,
        INotificationService notificationService, ServiceManager<ViewModelBase> dialogFactory)
    {
        this.logger = logger;
        this.settingsManager = settingsManager;
        this.packageFactory = packageFactory;
        this.notificationService = notificationService;
        this.dialogFactory = dialogFactory;

        Packages =
            new ObservableCollection<PackageCardViewModel>(
                settingsManager.Settings.InstalledPackages.Select(p =>
                    dialogFactory.Get<PackageCardViewModel>(vm => vm.Package = p)));
        
        EventManager.Instance.InstalledPackagesChanged += OnInstalledPackagesChanged;
    }

    [ObservableProperty] private ObservableCollection<PackageCardViewModel> packages;

    public override bool CanNavigateNext { get; protected set; } = true;
    public override bool CanNavigatePrevious { get; protected set; }
    public override string Title => "Packages";
    public override IconSource IconSource => new SymbolIconSource { Symbol = Symbol.Box, IsFilled = true};

    public override void OnLoaded()
    {
        Packages =
            new ObservableCollection<PackageCardViewModel>(
                settingsManager.Settings.InstalledPackages.Select(p =>
                    dialogFactory.Get<PackageCardViewModel>(vm => vm.Package = p)));
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
        OnLoaded();
    }
    
    private void OnInstalledPackagesChanged(object? sender, EventArgs e) => OnLoaded();
}
