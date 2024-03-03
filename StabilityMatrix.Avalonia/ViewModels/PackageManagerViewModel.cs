using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using FluentAvalonia.UI.Controls;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Avalonia.Animations;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.PackageManager;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Services;
using Symbol = FluentIcons.Common.Symbol;
using SymbolIconSource = FluentIcons.Avalonia.Fluent.SymbolIconSource;

namespace StabilityMatrix.Avalonia.ViewModels;

/// <summary>
///  This is our ViewModel for the second page
/// </summary>

[View(typeof(PackageManagerPage))]
[Singleton, ManagedService]
public partial class PackageManagerViewModel : PageViewModelBase
{
    private readonly ISettingsManager settingsManager;
    private readonly ServiceManager<ViewModelBase> dialogFactory;
    private readonly INotificationService notificationService;
    private readonly INavigationService<NewPackageManagerViewModel> packageNavigationService;
    private readonly ILogger<PackageManagerViewModel> logger;

    public override string Title => Resources.Label_Packages;
    public override IconSource IconSource => new SymbolIconSource { Symbol = Symbol.Box, IsFilled = true };

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

    private DispatcherTimer timer;

    public PackageManagerViewModel(
        ISettingsManager settingsManager,
        ServiceManager<ViewModelBase> dialogFactory,
        INotificationService notificationService,
        INavigationService<NewPackageManagerViewModel> packageNavigationService,
        ILogger<PackageManagerViewModel> logger
    )
    {
        this.settingsManager = settingsManager;
        this.dialogFactory = dialogFactory;
        this.notificationService = notificationService;
        this.packageNavigationService = packageNavigationService;
        this.logger = logger;

        EventManager.Instance.InstalledPackagesChanged += OnInstalledPackagesChanged;
        EventManager.Instance.OneClickInstallFinished += OnOneClickInstallFinished;

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

        timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(60), IsEnabled = true };
        timer.Tick += async (_, _) => await CheckPackagesForUpdates();
    }

    private void OnOneClickInstallFinished(object? sender, bool e)
    {
        OnLoadedAsync().SafeFireAndForget();
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

        installedPackages.EditDiff(settingsManager.Settings.InstalledPackages, InstalledPackage.Comparer);

        var currentUnknown = await Task.Run(IndexUnknownPackages);
        unknownInstalledPackages.Edit(s => s.Load(currentUnknown));

        timer.Start();
    }

    public override void OnUnloaded()
    {
        timer.Stop();
        base.OnUnloaded();
    }

    public void ShowInstallDialog(BasePackage? selectedPackage = null)
    {
        NavigateToSubPage(typeof(PackageInstallBrowserViewModel));
    }

    private async Task CheckPackagesForUpdates()
    {
        foreach (var package in PackageCards)
        {
            try
            {
                await package.OnLoadedAsync();
            }
            catch (Exception e)
            {
                logger.LogError(
                    e,
                    "Failed to check for updates for {Package}",
                    package?.Package?.PackageName
                );
            }
        }
    }

    private IEnumerable<UnknownInstalledPackage> IndexUnknownPackages()
    {
        var packageDir = settingsManager.LibraryDir.JoinDir("Packages");

        if (!packageDir.Exists)
        {
            yield break;
        }

        var currentPackages = settingsManager.Settings.InstalledPackages.ToImmutableArray();

        foreach (var subDir in packageDir.Info.EnumerateDirectories().Select(info => new DirectoryPath(info)))
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

    [RelayCommand]
    private void NavigateToSubPage(Type viewModelType)
    {
        Dispatcher.UIThread.Post(
            () =>
                packageNavigationService.NavigateTo(
                    viewModelType,
                    BetterSlideNavigationTransition.PageSlideFromRight
                ),
            DispatcherPriority.Send
        );
    }

    private void OnInstalledPackagesChanged(object? sender, EventArgs e) =>
        OnLoadedAsync().SafeFireAndForget();
}
