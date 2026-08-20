using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using NLog;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Core.Exceptions;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.PackageModification;
using StabilityMatrix.Core.Models.Packages.Extensions;

namespace StabilityMatrix.Avalonia.Helpers;

/// <summary>
/// Shared flow for prompting the user to install missing / out-of-date ComfyUI extensions
/// required by a workflow, then installing them and restarting the package.
/// Used by both Inference and Image Lab before queueing a prompt.
/// </summary>
public static class ComfyExtensionInstallHelper
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Shows a confirmation dialog listing the required extensions and, if accepted, installs
    /// them via the package modification runner and restarts the package. Must be called from
    /// the UI thread.
    /// </summary>
    /// <returns>True if the user accepted and the install was started.</returns>
    public static async Task<bool> PromptInstallAndRestartAsync(
        IPackageExtensionManager manager,
        PackagePair localPackagePair,
        IReadOnlyList<ExtensionSpecifier> missingExtensions,
        IReadOnlyList<(
            ExtensionSpecifier Specifier,
            InstalledPackageExtension Installed
        )> outOfDateExtensions,
        RunningPackageService runningPackageService,
        INotificationService notificationService
    )
    {
        var dialog = DialogHelper.CreateMarkdownDialog(
            $"#### The following extensions are required for this workflow:\n"
                + $"{string.Join("\n- ", missingExtensions.Select(ext => ext.Name))}"
                + $"{string.Join("\n- ", outOfDateExtensions.Select(pair => $"{pair.Specifier.Name} {pair.Specifier.Constraint} {pair.Specifier.Version} (Current Version: {pair.Installed.Version?.Tag})"))}",
            "Install Required Extensions?"
        );

        dialog.IsPrimaryButtonEnabled = true;
        dialog.DefaultButton = ContentDialogButton.Primary;
        dialog.PrimaryButtonText =
            $"{Resources.Action_Install} ({localPackagePair.InstalledPackage.DisplayName.ToRepr()} will restart)";
        dialog.CloseButtonText = Resources.Action_Cancel;

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return false;
        }

        var manifestExtensionsMap = await manager.GetManifestExtensionsMapAsync(
            manager.GetManifests(localPackagePair.InstalledPackage)
        );

        var steps = new List<IPackageStep>();

        // Add install for missing extensions
        foreach (var missingExtension in missingExtensions)
        {
            if (!manifestExtensionsMap.TryGetValue(missingExtension.Name, out var extension))
            {
                Logger.Warn("Extension {MissingExtensionUrl} not found in manifests", missingExtension.Name);
                continue;
            }

            steps.Add(new InstallExtensionStep(manager, localPackagePair.InstalledPackage, extension));
        }

        // Add update for out of date extensions
        foreach (var (specifier, installed) in outOfDateExtensions)
        {
            if (!manifestExtensionsMap.TryGetValue(specifier.Name, out _))
            {
                Logger.Warn("Extension {MissingExtensionUrl} not found in manifests", specifier.Name);
                continue;
            }

            steps.Add(new UpdateExtensionStep(manager, localPackagePair.InstalledPackage, installed));
        }

        var runner = new PackageModificationRunner
        {
            ShowDialogOnStart = true,
            ModificationCompleteTitle = "Extensions Installed",
            ModificationCompleteMessage = "Finished installing required extensions",
        };
        EventManager.Instance.OnPackageInstallProgressAdded(runner);

        runner
            .ExecuteSteps(steps)
            .ContinueWith(async _ =>
            {
                if (runner.Failed)
                    return;

                // Restart Package
                try
                {
                    await Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        await runningPackageService.StopPackage(localPackagePair.InstalledPackage.Id);
                        await runningPackageService.StartPackage(localPackagePair.InstalledPackage);
                    });
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Error while restarting package");

                    notificationService.ShowPersistent(
                        new AppException(
                            "Could not restart package",
                            "Please manually restart the package for extension changes to take effect"
                        )
                    );
                }
            })
            .SafeFireAndForget();

        return true;
    }
}
