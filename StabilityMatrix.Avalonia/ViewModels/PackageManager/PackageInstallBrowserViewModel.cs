using System;
using System.Reactive.Linq;
using System.Threading;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData;
using DynamicData.Alias;
using DynamicData.Binding;
using FluentAvalonia.UI.Controls;
using Injectio.Attributes;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Avalonia.Animations;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.PackageManager;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Analytics;
using StabilityMatrix.Core.Helper.Factory;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.PackageManager;

[View(typeof(PackageInstallBrowserView))]
[ManagedService]
[RegisterTransient<PackageInstallBrowserViewModel>]
public partial class PackageInstallBrowserViewModel(
    IPackageFactory packageFactory,
    INavigationService<PackageManagerViewModel> packageNavigationService,
    ISettingsManager settingsManager,
    INotificationService notificationService,
    ILogger<PackageInstallDetailViewModel> logger,
    IPrerequisiteHelper prerequisiteHelper,
    IAnalyticsHelper analyticsHelper,
    IPyInstallationManager pyInstallationManager
) : PageViewModelBase
{
    [ObservableProperty]
    private bool showIncompatiblePackages;

    [ObservableProperty]
    private string searchFilter = string.Empty;

    private SourceCache<BasePackage, string> packageSource = new(p => p.GithubUrl);

    public IObservableCollection<BasePackage> InferencePackages { get; } =
        new ObservableCollectionExtended<BasePackage>();

    public IObservableCollection<BasePackage> TrainingPackages { get; } =
        new ObservableCollectionExtended<BasePackage>();

    public IObservableCollection<BasePackage> LegacyPackages { get; } =
        new ObservableCollectionExtended<BasePackage>();

    public override string Title => "Add Package";
    public override IconSource IconSource => new SymbolIconSource { Symbol = Symbol.Add };

    protected override void OnInitialLoaded()
    {
        base.OnInitialLoaded();

        var incompatiblePredicate = this.WhenPropertyChanged(vm => vm.ShowIncompatiblePackages)
            .Select(_ => new Func<BasePackage, bool>(p => p.IsCompatible || ShowIncompatiblePackages))
            .ObserveOn(SynchronizationContext.Current)
            .AsObservable();

        var searchPredicate = this.WhenPropertyChanged(vm => vm.SearchFilter)
            .Select(_ => new Func<BasePackage, bool>(p =>
                p.DisplayName.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase)
            ))
            .ObserveOn(SynchronizationContext.Current)
            .AsObservable();

        packageSource
            .Connect()
            .DeferUntilLoaded()
            .Filter(incompatiblePredicate)
            .Filter(searchPredicate)
            .Where(p => p is { PackageType: PackageType.SdInference })
            .SortAndBind(
                InferencePackages,
                SortExpressionComparer<BasePackage>
                    .Ascending(p => p.InstallerSortOrder)
                    .ThenByAscending(p => p.DisplayName)
            )
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe();

        packageSource
            .Connect()
            .DeferUntilLoaded()
            .Filter(incompatiblePredicate)
            .Filter(searchPredicate)
            .Where(p => p is { PackageType: PackageType.SdTraining })
            .SortAndBind(
                TrainingPackages,
                SortExpressionComparer<BasePackage>
                    .Ascending(p => p.InstallerSortOrder)
                    .ThenByAscending(p => p.DisplayName)
            )
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe();

        packageSource
            .Connect()
            .DeferUntilLoaded()
            .Filter(incompatiblePredicate)
            .Filter(searchPredicate)
            .Where(p => p is { PackageType: PackageType.Legacy })
            .SortAndBind(
                LegacyPackages,
                SortExpressionComparer<BasePackage>
                    .Ascending(p => p.InstallerSortOrder)
                    .ThenByAscending(p => p.DisplayName)
            )
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe();

        packageSource.EditDiff(
            packageFactory.GetAllAvailablePackages(),
            (a, b) => a.GithubUrl == b.GithubUrl
        );
    }

    public void OnPackageSelected(BasePackage? package)
    {
        if (package is null)
        {
            return;
        }

        var vm = new PackageInstallDetailViewModel(
            package,
            settingsManager,
            notificationService,
            logger,
            prerequisiteHelper,
            packageNavigationService,
            packageFactory,
            analyticsHelper,
            pyInstallationManager
        );

        Dispatcher.UIThread.Post(
            () => packageNavigationService.NavigateTo(vm, BetterSlideNavigationTransition.PageSlideFromRight),
            DispatcherPriority.Send
        );
    }

    public void ClearSearchQuery()
    {
        SearchFilter = string.Empty;
    }
}
