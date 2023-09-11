using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using DynamicData;
using DynamicData.Binding;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Avalonia.ViewModels.PackageManager;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Factory;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.PackageModification;
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
    private readonly INotificationService notificationService;

    public override string Title => "Packages";
    public override IconSource IconSource =>
        new SymbolIconSource { Symbol = Symbol.Box, IsFilled = true };

    /// <summary>
    /// List of installed packages
    /// </summary>
    private readonly SourceCache<InstalledPackage, Guid> installedPackages = new(p => p.Id);

    /// <summary>
    /// List of indexed packages without a corresponding installed package
    /// </summary>
    private readonly SourceCache<InstalledPackage, Guid> unknownInstalledPackages = new(p => p.Id);

    public IObservableCollection<InstalledPackage> Packages { get; } =
        new ObservableCollectionExtended<InstalledPackage>();

    public IObservableCollection<PackageCardViewModel> PackageCards { get; } =
        new ObservableCollectionExtended<PackageCardViewModel>();

    public PackageManagerViewModel(
        ISettingsManager settingsManager,
        IPackageFactory packageFactory,
        ServiceManager<ViewModelBase> dialogFactory,
        INotificationService notificationService
    )
    {
        this.settingsManager = settingsManager;
        this.packageFactory = packageFactory;
        this.dialogFactory = dialogFactory;
        this.notificationService = notificationService;

        EventManager.Instance.InstalledPackagesChanged += OnInstalledPackagesChanged;

        var installed = installedPackages.Connect();
        var unknown = unknownInstalledPackages.Connect();

        installed
            .Or(unknown)
            .DeferUntilLoaded()
            .Bind(Packages)
            .Transform(
                p =>
                    dialogFactory.Get<PackageCardViewModel>(vm =>
                    {
                        vm.Package = p;
                        vm.OnLoadedAsync().SafeFireAndForget();
                    })
            )
            .Bind(PackageCards)
            .Subscribe();
    }

    public void SetPackages(IEnumerable<InstalledPackage> packages)
    {
        installedPackages.Edit(s => s.Load(packages));
    }

    public void SetUnknownPackages(IEnumerable<InstalledPackage> packages)
    {
        unknownInstalledPackages.Edit(s => s.Load(packages));
    }

    public override async Task OnLoadedAsync()
    {
        if (Design.IsDesignMode)
            return;

        installedPackages.EditDiff(
            settingsManager.Settings.InstalledPackages,
            InstalledPackage.Comparer
        );

        var currentUnknown = await Task.Run(IndexUnknownPackages);
        unknownInstalledPackages.Edit(s => s.Load(currentUnknown));
    }

    public async Task ShowInstallDialog()
    {
        var viewModel = dialogFactory.Get<InstallerViewModel>();
        viewModel.AvailablePackages = packageFactory.GetAllAvailablePackages().ToImmutableArray();
        viewModel.SelectedPackage = viewModel.AvailablePackages[0];

        var dialog = new BetterContentDialog
        {
            MaxDialogWidth = 900,
            MinDialogWidth = 900,
            DefaultButton = ContentDialogButton.Close,
            IsPrimaryButtonEnabled = false,
            IsSecondaryButtonEnabled = false,
            IsFooterVisible = false,
            Content = new InstallerDialog { DataContext = viewModel }
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            var runner = new PackageModificationRunner();
            var steps = viewModel.Steps;

            EventManager.Instance.OnPackageInstallProgressAdded(runner);
            await runner.ExecuteSteps(steps.ToList());
            EventManager.Instance.OnInstalledPackagesChanged();
            notificationService.Show(
                "Package Install Complete",
                $"{viewModel.InstallName} installed successfully",
                NotificationType.Success
            );
        }
    }

    private IEnumerable<UnknownInstalledPackage> IndexUnknownPackages()
    {
        var packageDir = new DirectoryPath(settingsManager.LibraryDir).JoinDir("Packages");

        if (!packageDir.Exists)
        {
            yield break;
        }

        var currentPackages = settingsManager.Settings.InstalledPackages.ToImmutableArray();

        foreach (
            var subDir in packageDir.Info
                .EnumerateDirectories()
                .Select(info => new DirectoryPath(info))
        )
        {
            var expectedLibraryPath = $"Packages{Path.DirectorySeparatorChar}{subDir.Name}";

            // Skip if the package is already installed
            if (currentPackages.Any(p => p.LibraryPath == expectedLibraryPath))
            {
                continue;
            }

            if (settingsManager.PackageInstallsInProgress.Contains(subDir.Name))
            {
                continue;
            }

            yield return UnknownInstalledPackage.FromDirectoryName(subDir.Name);
        }
    }

    private void OnInstalledPackagesChanged(object? sender, EventArgs e) =>
        OnLoadedAsync().SafeFireAndForget();
}
