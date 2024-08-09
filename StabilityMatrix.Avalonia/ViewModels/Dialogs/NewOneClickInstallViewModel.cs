using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Avalonia.Extensions;
using StabilityMatrix.Avalonia.Models.PackageSteps;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Factory;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.PackageModification;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

[Transient]
[ManagedService]
public partial class NewOneClickInstallViewModel : ContentDialogViewModelBase
{
    private readonly IPackageFactory packageFactory;
    private readonly ISettingsManager settingsManager;
    private readonly IPrerequisiteHelper prerequisiteHelper;
    private readonly ILogger<NewOneClickInstallViewModel> logger;
    private readonly IPyRunner pyRunner;
    private readonly INavigationService<MainWindowViewModel> navigationService;
    private readonly INotificationService notificationService;

    public SourceCache<BasePackage, string> AllPackagesCache { get; } = new(p => p.Author + p.Name);

    public IObservableCollection<BasePackage> ShownPackages { get; set; } =
        new ObservableCollectionExtended<BasePackage>();

    [ObservableProperty]
    private bool showIncompatiblePackages;

    private bool isInferenceInstall;

    public NewOneClickInstallViewModel(
        IPackageFactory packageFactory,
        ISettingsManager settingsManager,
        IPrerequisiteHelper prerequisiteHelper,
        ILogger<NewOneClickInstallViewModel> logger,
        IPyRunner pyRunner,
        INavigationService<MainWindowViewModel> navigationService,
        INotificationService notificationService
    )
    {
        this.packageFactory = packageFactory;
        this.settingsManager = settingsManager;
        this.prerequisiteHelper = prerequisiteHelper;
        this.logger = logger;
        this.pyRunner = pyRunner;
        this.navigationService = navigationService;
        this.notificationService = notificationService;

        var incompatiblePredicate = this.WhenPropertyChanged(vm => vm.ShowIncompatiblePackages)
            .Select(_ => new Func<BasePackage, bool>(p => p.IsCompatible || ShowIncompatiblePackages))
            .AsObservable();

        AllPackagesCache
            .Connect()
            .DeferUntilLoaded()
            .Filter(incompatiblePredicate)
            .Filter(p => p.OfferInOneClickInstaller)
            .Sort(
                SortExpressionComparer<BasePackage>
                    .Ascending(p => p.InstallerSortOrder)
                    .ThenByAscending(p => p.DisplayName)
            )
            .Bind(ShownPackages)
            .Subscribe();

        AllPackagesCache.AddOrUpdate(packageFactory.GetAllAvailablePackages());
        if (ShownPackages.Count > 0)
            return;

        ShowIncompatiblePackages = true;
    }

    [RelayCommand]
    private void InstallComfyForInference()
    {
        var comfyPackage = ShownPackages.FirstOrDefault(x => x is ComfyUI);
        if (comfyPackage == null)
            return;

        isInferenceInstall = true;
        InstallPackage(comfyPackage);
    }

    [RelayCommand]
    private void InstallPackage(BasePackage selectedPackage)
    {
        Task.Run(async () =>
            {
                var installLocation = Path.Combine(
                    settingsManager.LibraryDir,
                    "Packages",
                    selectedPackage.Name
                );

                var steps = new List<IPackageStep>
                {
                    new SetPackageInstallingStep(settingsManager, selectedPackage.Name),
                    new SetupPrerequisitesStep(prerequisiteHelper, pyRunner, selectedPackage),
                };

                // get latest version & download & install
                if (Directory.Exists(installLocation))
                {
                    var installPath = new DirectoryPath(installLocation);
                    await installPath.DeleteVerboseAsync(logger);
                }

                var downloadVersion = await selectedPackage.GetLatestVersion();
                var installedVersion = new InstalledPackageVersion { IsPrerelease = false };

                if (selectedPackage.ShouldIgnoreReleases)
                {
                    installedVersion.InstalledBranch = downloadVersion.BranchName;
                    installedVersion.InstalledCommitSha = downloadVersion.CommitHash;
                }
                else
                {
                    installedVersion.InstalledReleaseVersion = downloadVersion.VersionTag;
                }

                var torchVersion = selectedPackage.GetRecommendedTorchVersion();
                var recommendedSharedFolderMethod = selectedPackage.RecommendedSharedFolderMethod;

                var installedPackage = new InstalledPackage
                {
                    DisplayName = selectedPackage.DisplayName,
                    LibraryPath = Path.Combine("Packages", selectedPackage.Name),
                    Id = Guid.NewGuid(),
                    PackageName = selectedPackage.Name,
                    Version = installedVersion,
                    LaunchCommand = selectedPackage.LaunchCommand,
                    LastUpdateCheck = DateTimeOffset.Now,
                    PreferredTorchVersion = torchVersion,
                    PreferredSharedFolderMethod = recommendedSharedFolderMethod
                };

                var downloadStep = new DownloadPackageVersionStep(
                    selectedPackage,
                    installLocation,
                    new DownloadPackageOptions { VersionOptions = downloadVersion }
                );
                steps.Add(downloadStep);

                var unpackSiteCustomizeStep = new UnpackSiteCustomizeStep(
                    Path.Combine(installLocation, "venv")
                );
                steps.Add(unpackSiteCustomizeStep);

                var installStep = new InstallPackageStep(
                    selectedPackage,
                    installLocation,
                    installedPackage,
                    new InstallPackageOptions
                    {
                        SharedFolderMethod = recommendedSharedFolderMethod,
                        VersionOptions = downloadVersion,
                        PythonOptions = { TorchVersion = torchVersion }
                    }
                );
                steps.Add(installStep);

                var setupModelFoldersStep = new SetupModelFoldersStep(
                    selectedPackage,
                    recommendedSharedFolderMethod,
                    installLocation
                );
                steps.Add(setupModelFoldersStep);

                var addInstalledPackageStep = new AddInstalledPackageStep(settingsManager, installedPackage);
                steps.Add(addInstalledPackageStep);

                Dispatcher.UIThread.Post(() =>
                {
                    var runner = new PackageModificationRunner
                    {
                        ShowDialogOnStart = false,
                        HideCloseButton = false,
                        ModificationCompleteMessage = $"{selectedPackage.DisplayName} installed successfully"
                    };

                    runner
                        .ExecuteSteps(steps)
                        .ContinueWith(_ =>
                        {
                            notificationService.OnPackageInstallCompleted(runner);

                            EventManager.Instance.OnOneClickInstallFinished(false);

                            if (!isInferenceInstall)
                                return;

                            Dispatcher.UIThread.Post(() =>
                            {
                                navigationService.NavigateTo<InferenceViewModel>();
                            });
                        })
                        .SafeFireAndForget();

                    EventManager.Instance.OnPackageInstallProgressAdded(runner);
                });
            })
            .SafeFireAndForget();

        OnPrimaryButtonClick();
    }
}
