using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Dialogs;
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
    private readonly ISettingsManager settingsManager;
    private readonly IPrerequisiteHelper prerequisiteHelper;
    private readonly ILogger<NewOneClickInstallViewModel> logger;
    private readonly IPyRunner pyRunner;
    public SourceCache<BasePackage, string> AllPackagesCache { get; } = new(p => p.Author + p.Name);

    public IObservableCollection<BasePackage> ShownPackages { get; set; } =
        new ObservableCollectionExtended<BasePackage>();

    [ObservableProperty]
    private bool showIncompatiblePackages;

    public NewOneClickInstallViewModel(
        IPackageFactory packageFactory,
        ISettingsManager settingsManager,
        IPrerequisiteHelper prerequisiteHelper,
        ILogger<NewOneClickInstallViewModel> logger,
        IPyRunner pyRunner
    )
    {
        this.settingsManager = settingsManager;
        this.prerequisiteHelper = prerequisiteHelper;
        this.logger = logger;
        this.pyRunner = pyRunner;

        var incompatiblePredicate = this.WhenPropertyChanged(vm => vm.ShowIncompatiblePackages)
            .Select(_ => new Func<BasePackage, bool>(p => p.IsCompatible || ShowIncompatiblePackages))
            .AsObservable();

        AllPackagesCache
            .Connect()
            .DeferUntilLoaded()
            .Filter(incompatiblePredicate)
            .Filter(p => p.OfferInOneClickInstaller || ShowIncompatiblePackages)
            .Sort(
                SortExpressionComparer<BasePackage>
                    .Ascending(p => p.InstallerSortOrder)
                    .ThenByAscending(p => p.DisplayName)
            )
            .Bind(ShownPackages)
            .Subscribe();

        AllPackagesCache.AddOrUpdate(packageFactory.GetAllAvailablePackages());
    }

    [RelayCommand]
    private Task InstallComfyForInference()
    {
        var comfyPackage = ShownPackages.FirstOrDefault(x => x is ComfyUI);
        return comfyPackage != null ? InstallPackage(comfyPackage) : Task.CompletedTask;
    }

    [RelayCommand]
    private async Task InstallPackage(BasePackage selectedPackage)
    {
        var steps = new List<IPackageStep>
        {
            new SetPackageInstallingStep(settingsManager, selectedPackage.Name),
            new SetupPrerequisitesStep(prerequisiteHelper, pyRunner)
        };

        // get latest version & download & install
        var installLocation = Path.Combine(settingsManager.LibraryDir, "Packages", selectedPackage.Name);
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

        var downloadStep = new DownloadPackageVersionStep(selectedPackage, installLocation, downloadVersion);
        steps.Add(downloadStep);

        var installStep = new InstallPackageStep(
            selectedPackage,
            torchVersion,
            recommendedSharedFolderMethod,
            downloadVersion,
            installLocation
        );
        steps.Add(installStep);

        var setupModelFoldersStep = new SetupModelFoldersStep(
            selectedPackage,
            recommendedSharedFolderMethod,
            installLocation
        );
        steps.Add(setupModelFoldersStep);

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

        var addInstalledPackageStep = new AddInstalledPackageStep(settingsManager, installedPackage);
        steps.Add(addInstalledPackageStep);

        var runner = new PackageModificationRunner { ShowDialogOnStart = false, HideCloseButton = false, };
        EventManager.Instance.OnAddPackageInstallWithoutBlocking(this, runner, steps);

        OnPrimaryButtonClick();
    }
}
