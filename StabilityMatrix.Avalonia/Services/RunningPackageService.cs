﻿using System.Collections.Immutable;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentAvalonia.UI.Controls;
using Injectio.Attributes;
using KeyedSemaphores;
using Microsoft.Extensions.Logging;
using Nito.Disposables.Internals;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.ViewModels;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper.Factory;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.Services;

[RegisterSingleton<RunningPackageService>]
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

        // Show warning if critical vulnerabilities are found
        if (basePackage.HasCriticalVulnerabilities)
        {
            var vulns = basePackage
                .KnownVulnerabilities.Where(v => v.Severity == VulnerabilitySeverity.Critical)
                .Select(v =>
                    $"**{v.Id}**: {v.Title}\n  - Severity: {v.Severity}\n  - Description: {v.Description}"
                )
                .ToList();

            var message =
                $"# ⚠️ Critical Security Vulnerabilities\n\nThis package has critical security vulnerabilities that may put your system at risk:\n\n{string.Join("\n\n", vulns)}";
            message +=
                "\n\nFor more information, please visit the [GitHub Security Advisory page](https://github.com/LykosAI/StabilityMatrix/security/advisories).";

            var dialog = DialogHelper.CreateMarkdownDialog(message, "Security Warning");

            dialog.IsPrimaryButtonEnabled = false;
            dialog.PrimaryButtonText = "Continue Anyway (3)";
            dialog.CloseButtonText = Resources.Action_Cancel;
            dialog.DefaultButton = ContentDialogButton.Close;

            // Start a timer to enable the button after 3 seconds
            var countdown = 3;
            var timer = new System.Timers.Timer(1000);
            timer.Elapsed += (_, _) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    countdown--;
                    if (countdown <= 0)
                    {
                        dialog.IsPrimaryButtonEnabled = true;
                        dialog.PrimaryButtonText = "Continue Anyway";
                        timer.Stop();
                        timer.Dispose();
                    }
                    else
                    {
                        dialog.PrimaryButtonText = $"Continue Anyway ({countdown})";
                    }
                });
            };
            timer.Start();

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                return null;
            }
        }
        // Show warning if any vulnerabilities are found
        else if (basePackage.HasVulnerabilities)
        {
            var vulns = basePackage
                .KnownVulnerabilities.Select(v =>
                    $"**{v.Id}**: {v.Title}\n  - Severity: {v.Severity}\n  - Description: {v.Description}"
                )
                .ToList();

            var message =
                $"# ⚠️ Security Notice\n\nThis package has known vulnerabilities:\n\n{string.Join("\n\n", vulns)}";

            message +=
                "\n\nFor more information, please visit the [GitHub Security Advisory page](https://github.com/LykosAI/StabilityMatrix/security/advisories).";

            var dialog = DialogHelper.CreateMarkdownDialog(message, "Security Notice");

            dialog.IsPrimaryButtonEnabled = false;
            dialog.PrimaryButtonText = "Continue Anyway (3)";
            dialog.CloseButtonText = Resources.Action_Cancel;
            dialog.DefaultButton = ContentDialogButton.Close;

            // Start a timer to enable the button after 3 seconds
            var countdown = 3;
            var timer = new System.Timers.Timer(1000);
            timer.Elapsed += (_, _) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    countdown--;
                    if (countdown <= 0)
                    {
                        dialog.IsPrimaryButtonEnabled = true;
                        dialog.PrimaryButtonText = "Continue Anyway";
                        timer.Stop();
                        timer.Dispose();
                    }
                    else
                    {
                        dialog.PrimaryButtonText = $"Continue Anyway ({countdown})";
                    }
                });
            };
            timer.Start();

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                return null;
            }
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
        var runPackageOptions = new RunPackageOptions { Command = command, Arguments = launchProcessArgs };

        // Join with extras, if any
        await basePackage.RunPackage(
            packagePath,
            installedPackage,
            runPackageOptions,
            console.Post,
            cancellationToken
        );

        var runningPackage = new PackagePair(installedPackage, basePackage);

        var viewModel = new RunningPackageViewModel(
            settingsManager,
            notificationService,
            this,
            runningPackage,
            runPackageOptions,
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
