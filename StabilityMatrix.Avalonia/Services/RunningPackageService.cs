using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls.Notifications;
using CommunityToolkit.Mvvm.ComponentModel;
using KeyedSemaphores;
using Microsoft.Extensions.Logging;
using Nito.Disposables.Internals;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.ViewModels;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper.Factory;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Processes;
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
) : ObservableObject, IDisposable
{
    /// <summary>
    /// Locks for starting or stopping packages.
    /// </summary>
    private readonly KeyedSemaphoresDictionary<Guid> packageLocks = new();

    // 🤔 what if we put the ConsoleViewModel inside the BasePackage? 🤔
    [ObservableProperty]
    private ObservableDictionary<Guid, RunningPackageViewModel> runningPackages = [];

    public async Task<PackagePair?> StartPackage(
        InstalledPackage installedPackage,
        string? command = null,
        CancellationToken cancellationToken = default
    )
    {
        // Get lock
        using var @lock = await packageLocks.LockAsync(installedPackage.Id, cancellationToken);

        // Ignore if already running after lock
        if (RunningPackages.ContainsKey(installedPackage.Id))
        {
            logger.LogWarning("Skipping StartPackage, already running: {Id}", installedPackage.Id);
            return null;
        }

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

        if (installedPackage.UseSharedOutputFolder)
        {
            await basePackage.SetupOutputFolderLinks(installedPackage.FullPath!);
        }

        // Load user launch args from settings
        var launchArgStrings = (installedPackage.LaunchArgs ?? [])
            .Select(option => option.ToArgString())
            .WhereNotNull()
            .ToArray();

        var launchProcessArgs = ProcessArgs.FromQuoted(launchArgStrings);

        // Join with extras, if any
        await basePackage.RunPackage(
            packagePath,
            installedPackage,
            new RunPackageOptions { Command = command, Arguments = launchProcessArgs },
            console.Post,
            cancellationToken
        );

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

    public async Task StopPackage(Guid id, CancellationToken cancellationToken = default)
    {
        // Get lock
        using var @lock = await packageLocks.LockAsync(id, cancellationToken);

        // Ignore if not running after lock
        if (!RunningPackages.TryGetValue(id, out var vm))
        {
            logger.LogWarning("Skipping StopPackage, not running: {Id}", id);
            return;
        }

        var runningPackage = vm.RunningPackage;
        await runningPackage.BasePackage.WaitForShutdown();

        await vm.DisposeAsync();

        RunningPackages.Remove(id);
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

    public void Dispose()
    {
        var exceptions = new List<Exception>();

        foreach (var (_, vm) in RunningPackages)
        {
            try
            {
                vm.Dispose();
            }
            catch (Exception e)
            {
                exceptions.Add(e);
            }
        }

        if (exceptions.Count != 0)
        {
            throw new AggregateException(exceptions);
        }

        GC.SuppressFinalize(this);
    }
}
