using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData;
using DynamicData.Alias;
using DynamicData.Binding;
using FluentAvalonia.UI.Controls;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Avalonia.Animations;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Factory;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

[View(typeof(NewInstallerDialog))]
[Transient, ManagedService]
public partial class NewInstallerDialogViewModel : PageViewModelBase
{
    private readonly INavigationService<NewPackageManagerViewModel> packageNavigationService;
    private readonly ISettingsManager settingsManager;
    private readonly INotificationService notificationService;
    private readonly ILogger<PackageInstallDetailViewModel> logger;
    private readonly IPyRunner pyRunner;
    private readonly IPrerequisiteHelper prerequisiteHelper;

    [ObservableProperty]
    private bool showIncompatiblePackages = false;

    private SourceCache<BasePackage, string> packageSource = new(p => p.GithubUrl);

    public IObservableCollection<BasePackage> InferencePackages { get; } =
        new ObservableCollectionExtended<BasePackage>();

    public IObservableCollection<BasePackage> TrainingPackages { get; } =
        new ObservableCollectionExtended<BasePackage>();

    public NewInstallerDialogViewModel(
        IPackageFactory packageFactory,
        INavigationService<NewPackageManagerViewModel> packageNavigationService,
        ISettingsManager settingsManager,
        INotificationService notificationService,
        ILogger<PackageInstallDetailViewModel> logger,
        IPyRunner pyRunner,
        IPrerequisiteHelper prerequisiteHelper
    )
    {
        this.packageNavigationService = packageNavigationService;
        this.settingsManager = settingsManager;
        this.notificationService = notificationService;
        this.logger = logger;
        this.pyRunner = pyRunner;
        this.prerequisiteHelper = prerequisiteHelper;

        var searchPredicate = this.WhenPropertyChanged(vm => vm.ShowIncompatiblePackages)
            .Select(_ => new Func<BasePackage, bool>(p => p.IsCompatible || ShowIncompatiblePackages))
            .AsObservable();

        packageSource
            .Connect()
            .DeferUntilLoaded()
            .Filter(searchPredicate)
            .Where(p => p is { PackageType: PackageType.SdInference })
            .Sort(
                SortExpressionComparer<BasePackage>
                    .Ascending(p => p.InstallerSortOrder)
                    .ThenByAscending(p => p.DisplayName)
            )
            .Bind(InferencePackages)
            .Subscribe();

        packageSource
            .Connect()
            .DeferUntilLoaded()
            .Filter(searchPredicate)
            .Where(p => p is { PackageType: PackageType.SdTraining })
            .Sort(
                SortExpressionComparer<BasePackage>
                    .Ascending(p => p.InstallerSortOrder)
                    .ThenByAscending(p => p.DisplayName)
            )
            .Bind(TrainingPackages)
            .Subscribe();

        packageSource.EditDiff(
            packageFactory.GetAllAvailablePackages(),
            (a, b) => a.GithubUrl == b.GithubUrl
        );
    }

    public override string Title => "Add Package";
    public override IconSource IconSource => new SymbolIconSource { Symbol = Symbol.Add };

    public void OnPackageSelected(BaseGitPackage package)
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
            pyRunner,
            prerequisiteHelper,
            packageNavigationService
        );

        Dispatcher.UIThread.Post(
            () => packageNavigationService.NavigateTo(vm, BetterSlideNavigationTransition.PageSlideFromRight),
            DispatcherPriority.Send
        );
    }
}
