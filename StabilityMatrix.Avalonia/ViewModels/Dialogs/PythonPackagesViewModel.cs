using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using AutoCtor;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using FluentAvalonia.UI.Controls;
using Injectio.Attributes;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.PackageModification;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

[View(typeof(PythonPackagesDialog))]
[ManagedService]
[RegisterTransient<PythonPackagesViewModel>]
[AutoConstruct]
public partial class PythonPackagesViewModel : ContentDialogViewModelBase
{
    private readonly ILogger<PythonPackagesViewModel> logger;
    private readonly ISettingsManager settingsManager;
    private readonly IPyInstallationManager pyInstallationManager;
    private PyBaseInstall? pyBaseInstall;

    public DirectoryPath? VenvPath { get; set; }

    public PyVersion? PythonVersion { get; set; }

    [ObservableProperty]
    private bool isLoading;

    private readonly SourceCache<PipPackageInfo, string> packageSource = new(p => p.Name);

    public IObservableCollection<PythonPackagesItemViewModel> Packages { get; } =
        new ObservableCollectionExtended<PythonPackagesItemViewModel>();

    [ObservableProperty]
    private PythonPackagesItemViewModel? selectedPackage;

    [ObservableProperty]
    private string searchQuery = string.Empty;

    [AutoPostConstruct]
    private void PostConstruct()
    {
        var searchPredicate = this.WhenPropertyChanged(vm => vm.SearchQuery)
            .Throttle(TimeSpan.FromMilliseconds(100))
            .DistinctUntilChanged()
            .Select(value =>
            {
                if (string.IsNullOrWhiteSpace(value.Value))
                {
                    return (static _ => true);
                }

                return (Func<PipPackageInfo, bool>)(
                    p => p.Name.Contains(value.Value, StringComparison.OrdinalIgnoreCase)
                );
            });

        packageSource
            .Connect()
            .DeferUntilLoaded()
            .Filter(searchPredicate)
            .Transform(p => new PythonPackagesItemViewModel(settingsManager) { Package = p })
            .SortBy(vm => vm.Package.Name)
            .Bind(Packages)
            .ObserveOn(SynchronizationContext.Current!)
            .Subscribe();
    }

    private async Task Refresh()
    {
        if (VenvPath is null)
            return;

        IsLoading = true;

        try
        {
            if (Design.IsDesignMode)
            {
                await Task.Delay(250);
            }
            else
            {
                pyBaseInstall ??= new PyBaseInstall(
                    pyInstallationManager.GetInstallation(
                        PythonVersion ?? PyInstallationManager.Python_3_10_11
                    )
                );

                var envVars = new Dictionary<string, string>();
                if (pyBaseInstall.Version == PyInstallationManager.Python_3_10_11)
                {
                    envVars["SETUPTOOLS_USE_DISTUTILS"] = "stdlib";
                }

                envVars.Update(settingsManager.Settings.EnvironmentVariables);

                await using var venvRunner = await pyBaseInstall.CreateVenvRunnerAsync(
                    VenvPath,
                    workingDirectory: VenvPath.Parent,
                    environmentVariables: envVars
                );

                var packages = await venvRunner.PipList();

                packageSource.EditDiff(packages);

                // Delay a bit to prevent thread issues with UI list
                await Task.Delay(100);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshBackground()
    {
        if (VenvPath is null)
            return;

        pyBaseInstall ??= new PyBaseInstall(
            pyInstallationManager.GetInstallation(PythonVersion ?? PyInstallationManager.Python_3_10_11)
        );
        await using var venvRunner = await pyBaseInstall.CreateVenvRunnerAsync(
            VenvPath,
            workingDirectory: VenvPath.Parent,
            environmentVariables: settingsManager.Settings.EnvironmentVariables
        );

        var packages = await venvRunner.PipList();

        Dispatcher.UIThread.Post(() =>
        {
            // Backup selected package
            var currentPackageName = SelectedPackage?.Package.Name;

            packageSource.EditDiff(packages);

            // Restore selected package
            SelectedPackage = Packages.FirstOrDefault(p => p.Package.Name == currentPackageName);
        });
    }

    /// <summary>
    /// Load the selected package's show info if not already loaded
    /// </summary>
    partial void OnSelectedPackageChanged(PythonPackagesItemViewModel? value)
    {
        if (value is null)
        {
            return;
        }

        if (value.PipShowResult is null)
        {
            value.LoadExtraInfo(VenvPath!).SafeFireAndForget();
        }
    }

    /// <inheritdoc />
    public override Task OnLoadedAsync()
    {
        return Refresh();
    }

    public void AddPackages(params PipPackageInfo[] packages)
    {
        packageSource.AddOrUpdate(packages);
    }

    [RelayCommand]
    private Task ModifySelectedPackage(PythonPackagesItemViewModel? item)
    {
        return item?.SelectedVersion != null
            ? UpgradePackageVersion(
                item.Package.Name,
                item.SelectedVersion,
                PythonPackagesItemViewModel.GetKnownIndexUrl(item.Package.Name, item.SelectedVersion),
                isDowngrade: item.CanDowngrade
            )
            : Task.CompletedTask;
    }

    private async Task UpgradePackageVersion(
        string packageName,
        string version,
        string? extraIndexUrl = null,
        bool isDowngrade = false
    )
    {
        if (VenvPath is null || SelectedPackage?.Package is not { } package)
            return;

        // Confirmation dialog
        var dialog = DialogHelper.CreateMarkdownDialog(
            isDowngrade
                ? $"Downgrade **{package.Name}** to **{version}**?"
                : $"Upgrade **{package.Name}** to **{version}**?",
            Resources.Label_ConfirmQuestion
        );

        dialog.PrimaryButtonText = isDowngrade ? Resources.Action_Downgrade : Resources.Action_Upgrade;
        dialog.IsPrimaryButtonEnabled = true;
        dialog.DefaultButton = ContentDialogButton.Primary;
        dialog.CloseButtonText = Resources.Action_Cancel;

        if (await dialog.ShowAsync() is not ContentDialogResult.Primary)
        {
            return;
        }

        var args = new ProcessArgsBuilder("install", $"{packageName}=={version}");

        if (extraIndexUrl != null)
        {
            args = args.AddArg(("--extra-index-url", extraIndexUrl));
        }

        var steps = new List<IPackageStep>
        {
            new PipStep
            {
                VenvDirectory = VenvPath,
                WorkingDirectory = VenvPath.Parent,
                Args = args,
                BaseInstall = pyBaseInstall
            }
        };

        var runner = new PackageModificationRunner
        {
            ShowDialogOnStart = true,
            ModificationCompleteMessage = isDowngrade
                ? $"Downgraded Python Package '{packageName}' to {version}"
                : $"Upgraded Python Package '{packageName}' to {version}"
        };
        EventManager.Instance.OnPackageInstallProgressAdded(runner);
        await runner.ExecuteSteps(steps);

        // Refresh
        RefreshBackground().SafeFireAndForget();
    }

    [RelayCommand]
    private async Task InstallPackage()
    {
        if (VenvPath is null)
            return;

        // Dialog
        var fields = new TextBoxField[]
        {
            new() { Label = "Package Name", InnerLeftText = "pip install" }
        };

        var dialog = DialogHelper.CreateTextEntryDialog("Install Package", "", fields);
        var result = await dialog.ShowAsync();

        if (result != ContentDialogResult.Primary || fields[0].Text is not { } packageArgs)
        {
            return;
        }

        var steps = new List<IPackageStep>
        {
            new PipStep
            {
                VenvDirectory = VenvPath,
                WorkingDirectory = VenvPath.Parent,
                Args = new ProcessArgs(packageArgs).Prepend("install"),
                BaseInstall = pyBaseInstall,
                EnvironmentVariables = settingsManager.Settings.EnvironmentVariables
            }
        };

        var runner = new PackageModificationRunner
        {
            ShowDialogOnStart = true,
            ModificationCompleteMessage = $"Installed Python Package '{packageArgs}'"
        };
        EventManager.Instance.OnPackageInstallProgressAdded(runner);
        await runner.ExecuteSteps(steps);

        // Refresh
        RefreshBackground().SafeFireAndForget();
    }

    [RelayCommand]
    private async Task UninstallSelectedPackage()
    {
        if (VenvPath is null || SelectedPackage?.Package is not { } package)
            return;

        // Confirmation dialog
        var dialog = DialogHelper.CreateMarkdownDialog(
            $"This will uninstall the package '{package.Name}'",
            Resources.Label_ConfirmQuestion
        );
        dialog.PrimaryButtonText = Resources.Action_Uninstall;
        dialog.IsPrimaryButtonEnabled = true;
        dialog.DefaultButton = ContentDialogButton.Primary;
        dialog.CloseButtonText = Resources.Action_Cancel;

        if (await dialog.ShowAsync() is not ContentDialogResult.Primary)
        {
            return;
        }

        var steps = new List<IPackageStep>
        {
            new PipStep
            {
                VenvDirectory = VenvPath,
                WorkingDirectory = VenvPath.Parent,
                Args = new[] { "uninstall", "--yes", package.Name },
                BaseInstall = pyBaseInstall
            }
        };

        var runner = new PackageModificationRunner
        {
            ShowDialogOnStart = true,
            ModificationCompleteMessage = $"Uninstalled Python Package '{package.Name}'"
        };
        EventManager.Instance.OnPackageInstallProgressAdded(runner);
        await runner.ExecuteSteps(steps);

        // Refresh
        RefreshBackground().SafeFireAndForget();
    }

    public override BetterContentDialog GetDialog()
    {
        return new BetterContentDialog
        {
            CloseOnClickOutside = true,
            MinDialogWidth = 800,
            MaxDialogWidth = 1500,
            FullSizeDesired = true,
            ContentVerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Title = Resources.Label_PythonPackages,
            Content = new PythonPackagesDialog { DataContext = this },
            CloseButtonText = Resources.Action_Close,
            DefaultButton = ContentDialogButton.Close
        };
    }
}
