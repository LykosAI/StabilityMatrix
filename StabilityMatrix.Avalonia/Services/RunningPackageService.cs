using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.Notifications;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.ViewModels;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper.Factory;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.Services;

[Singleton]
public partial class RunningPackageService(
    ILogger<RunningPackageService> logger,
    IPackageFactory packageFactory,
    INotificationService notificationService,
    ISettingsManager settingsManager,
    IPyRunner pyRunner
) : ObservableObject
{
    // 🤔 what if we put the ConsoleViewModel inside the BasePackage? 🤔
    [ObservableProperty]
    private ObservableDictionary<Guid, RunningPackageViewModel> runningPackages = [];

    public async Task<PackagePair?> StartPackage(InstalledPackage installedPackage, string? command = null)
    {
        var activeInstallName = installedPackage.PackageName;
        var basePackage = string.IsNullOrWhiteSpace(activeInstallName)
            ? null
            : packageFactory.GetNewBasePackage(installedPackage);

        if (basePackage == null)
        {
            logger.LogWarning(
                "During launch, package name '{PackageName}' did not match a definition",
                activeInstallName
            );

            notificationService.Show(
                new Notification(
                    "Package name invalid",
                    "Install package name did not match a definition. Please reinstall and let us know about this issue.",
                    NotificationType.Error
                )
            );
            return null;
        }

        // If this is the first launch (LaunchArgs is null),
        // load and save a launch options dialog vm
        // so that dynamic initial values are saved.
        if (installedPackage.LaunchArgs == null)
        {
            var definitions = basePackage.LaunchOptions;
            // Create config cards and save them
            var cards = LaunchOptionCard
                .FromDefinitions(definitions, Array.Empty<LaunchOption>())
                .ToImmutableArray();

            var args = cards.SelectMany(c => c.Options).ToList();

            logger.LogDebug(
                "Setting initial launch args: {Args}",
                string.Join(", ", args.Select(o => o.ToArgString()?.ToRepr()))
            );

            settingsManager.SaveLaunchArgs(installedPackage.Id, args);
        }

        if (basePackage is not StableSwarm)
        {
            await pyRunner.Initialize();
        }

        // Get path from package
        var packagePath = new DirectoryPath(settingsManager.LibraryDir, installedPackage.LibraryPath!);

        if (basePackage is not StableSwarm)
        {
            // Unpack sitecustomize.py to venv
            await UnpackSiteCustomize(packagePath.JoinDir("venv"));
        }

        // Clear console and start update processing
        var console = new ConsoleViewModel();
        console.StartUpdates();

        // Update shared folder links (in case library paths changed)
        await basePackage.UpdateModelFolders(
            packagePath,
            installedPackage.PreferredSharedFolderMethod ?? basePackage.RecommendedSharedFolderMethod
        );

        // Load user launch args from settings and convert to string
        var userArgs = installedPackage.LaunchArgs ?? [];
        var userArgsString = string.Join(" ", userArgs.Select(opt => opt.ToArgString()));

        // Join with extras, if any
        userArgsString = string.Join(" ", userArgsString, basePackage.ExtraLaunchArguments);

        // Use input command if provided, otherwise use package launch command
        command ??= basePackage.LaunchCommand;

        await basePackage.RunPackage(packagePath, command, userArgsString, o => console.Post(o));
        var runningPackage = new PackagePair(installedPackage, basePackage);

        var viewModel = new RunningPackageViewModel(
            settingsManager,
            notificationService,
            this,
            runningPackage,
            console
        );
        RunningPackages.Add(runningPackage.InstalledPackage.Id, viewModel);

        return runningPackage;
    }

    public async Task StopPackage(Guid id)
    {
        if (RunningPackages.TryGetValue(id, out var vm))
        {
            var runningPackage = vm.RunningPackage;
            await runningPackage.BasePackage.WaitForShutdown();
            RunningPackages.Remove(id);
        }
    }

    public RunningPackageViewModel? GetRunningPackageViewModel(Guid id) =>
        RunningPackages.TryGetValue(id, out var vm) ? vm : null;

    private static async Task UnpackSiteCustomize(DirectoryPath venvPath)
    {
        var sitePackages = venvPath.JoinDir(PyVenvRunner.RelativeSitePackagesPath);
        var file = sitePackages.JoinFile("sitecustomize.py");
        file.Directory?.Create();
        await Assets.PyScriptSiteCustomize.ExtractTo(file, true);
    }
}
