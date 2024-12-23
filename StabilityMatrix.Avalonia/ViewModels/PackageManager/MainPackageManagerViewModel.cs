using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using FluentAvalonia.UI.Controls;
using FluentIcons.Common;
using Injectio.Attributes;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Avalonia.Animations;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Services;
using MainPackageManagerView = StabilityMatrix.Avalonia.Views.PackageManager.MainPackageManagerView;
using Symbol = FluentIcons.Common.Symbol;
using SymbolIconSource = FluentIcons.Avalonia.Fluent.SymbolIconSource;

namespace StabilityMatrix.Avalonia.ViewModels.PackageManager;

/// <summary>
///  This is our ViewModel for the second page
/// </summary>

[View(typeof(MainPackageManagerView))]
[ManagedService]
[RegisterSingleton<MainPackageManagerViewModel>]
public partial class MainPackageManagerViewModel : PageViewModelBase
{
    private readonly ISettingsManager settingsManager;
    private readonly ServiceManager<ViewModelBase> dialogFactory;
    private readonly INotificationService notificationService;
    private readonly INavigationService<PackageManagerViewModel> packageNavigationService;
    private readonly ILogger<MainPackageManagerViewModel> logger;
    private readonly RunningPackageService runningPackageService;

    public override string Title => Resources.Label_Packages;
    public override IconSource IconSource =>
        new SymbolIconSource { Symbol = Symbol.Box, IconVariant = IconVariant.Filled };

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

    public MainPackageManagerViewModel(
        ISettingsManager settingsManager,
        ServiceManager<ViewModelBase> dialogFactory,
        INotificationService notificationService,
        INavigationService<PackageManagerViewModel> packageNavigationService,
        ILogger<MainPackageManagerViewModel> logger,
        RunningPackageService runningPackageService
    )
    {
        this.settingsManager = settingsManager;
        this.dialogFactory = dialogFactory;
        this.notificationService = notificationService;
        this.packageNavigationService = packageNavigationService;
        this.logger = logger;
        this.runningPackageService = runningPackageService;

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
            .ObserveOn(SynchronizationContext.Current)
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

    protected override async Task OnInitialLoadedAsync()
    {
        if (string.IsNullOrWhiteSpace(Program.Args.LaunchPackageName))
        {
            await base.OnInitialLoadedAsync();
            return;
        }

        await LoadPackages();

        var package = Packages.FirstOrDefault(x => x.DisplayName == Program.Args.LaunchPackageName);
        if (package is not null)
        {
            await runningPackageService.StartPackage(package);
            return;
        }

        package = Packages.FirstOrDefault(x => x.Id.ToString() == Program.Args.LaunchPackageName);
        if (package is null)
        {
            await base.OnInitialLoadedAsync();
            return;
        }

        await runningPackageService.StartPackage(package);
    }

    public override async Task OnLoadedAsync()
    {
        if (Design.IsDesignMode || !settingsManager.IsLibraryDirSet)
            return;

        await LoadPackages();

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

    private async Task LoadPackages()
    {
        installedPackages.EditDiff(settingsManager.Settings.InstalledPackages, InstalledPackage.Comparer);

        var currentUnknown = await Task.Run(IndexUnknownPackages);
        unknownInstalledPackages.Edit(s => s.Load(currentUnknown));
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
