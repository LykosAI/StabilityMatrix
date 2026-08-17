using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentAvalonia.UI.Controls;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Factory;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.PackageModification;
using StabilityMatrix.Core.Models.Packages.Extensions;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

/// <summary>
/// Shows the custom node packs a set of workflow files requires, with install
/// options for packs that are missing but resolvable via the extension index.
/// </summary>
[View(typeof(WorkflowNodesDialog))]
[ManagedService]
[RegisterTransient<WorkflowNodesDialogViewModel>]
public partial class WorkflowNodesDialogViewModel(
    ISettingsManager settingsManager,
    IPackageFactory packageFactory
) : ContentDialogViewModelBase
{
    public required IReadOnlyList<FilePath> WorkflowFiles { get; init; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoPacks))]
    public partial ObservableCollection<WorkflowNodePackViewModel> Packs { get; set; } = [];

    [ObservableProperty]
    public partial InstalledPackage? SelectedPackage { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoPacks))]
    public partial bool IsLoading { get; set; }

    public List<InstalledPackage> AvailablePackages =>
        settingsManager
            .Settings.InstalledPackages.Where(package => package.PackageName is "ComfyUI" or "ComfyUI-Zluda")
            .ToList();

    public PackagePair? SelectedPackagePair =>
        SelectedPackage is { } package ? packageFactory.GetPackagePair(package) : null;

    /// <summary>
    /// True when analysis finished and the workflows need no custom nodes at all.
    /// </summary>
    public bool HasNoPacks => !IsLoading && Packs.Count == 0;

    /// <summary>
    /// Parses the given workflow files and returns whether any of them reference
    /// custom node packs, without any UI. Used to skip the dialog after imports
    /// of workflows that only use built-in nodes.
    /// </summary>
    public static async Task<bool> HasRequiredPacksAsync(IEnumerable<FilePath> workflowFiles)
    {
        foreach (var file in workflowFiles.Where(f => f.Exists))
        {
            try
            {
                if (
                    JsonNode.Parse(await File.ReadAllTextAsync(file)) is JsonObject workflow
                    && WorkflowNodeAnalyzer.GetRequiredPacks(workflow).Count > 0
                )
                {
                    return true;
                }
            }
            catch (Exception)
            {
                // Unparseable files can't tell us anything
            }
        }

        return false;
    }

    private bool isInitialized;

    public override async Task OnLoadedAsync()
    {
        if (Design.IsDesignMode)
            return;

        await EnsureInitializedAsync();
    }

    /// <summary>
    /// Runs the pack analysis if it hasn't run yet. Callers may use this before showing
    /// the dialog to decide whether showing it is worthwhile.
    /// </summary>
    public async Task EnsureInitializedAsync()
    {
        if (isInitialized)
            return;

        isInitialized = true;

        SelectedPackage =
            settingsManager.Settings.PreferredWorkflowPackage ?? AvailablePackages.FirstOrDefault();

        await AnalyzeAsync();
    }

    partial void OnSelectedPackageChanged(InstalledPackage? oldValue, InstalledPackage? newValue)
    {
        if (oldValue is null)
            return;

        settingsManager.Transaction(settings =>
        {
            settings.PreferredWorkflowPackage = newValue;
        });

        AnalyzeAsync().SafeFireAndForget();
    }

    /// <summary>
    /// The install steps for the packs the user selected, for the caller to run.
    /// </summary>
    public List<IPackageStep> GetInstallSteps()
    {
        if (SelectedPackagePair is not { BasePackage.ExtensionManager: { } extensionManager } pair)
            return [];

        return Packs
            .Where(pack => pack is { IsSelected: true, Extension: not null })
            .Select(pack =>
                (IPackageStep)
                    new InstallExtensionStep(extensionManager, pair.InstalledPackage, pack.Extension!)
            )
            .ToList();
    }

    private async Task AnalyzeAsync()
    {
        IsLoading = true;

        try
        {
            var requiredPacks = new Dictionary<string, WorkflowNodePack>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in WorkflowFiles.Where(f => f.Exists))
            {
                JsonObject? workflow;
                try
                {
                    workflow = JsonNode.Parse(await File.ReadAllTextAsync(file)) as JsonObject;
                }
                catch (Exception)
                {
                    continue;
                }

                if (workflow is null)
                    continue;

                foreach (var pack in WorkflowNodeAnalyzer.GetRequiredPacks(workflow))
                {
                    var key = pack.CnrId ?? pack.AuxId ?? "unidentified";
                    if (requiredPacks.TryGetValue(key, out var existing))
                    {
                        existing.NodeTypes.UnionWith(pack.NodeTypes);
                    }
                    else
                    {
                        requiredPacks[key] = pack;
                    }
                }
            }

            var manifestExtensions = new List<PackageExtension>();
            var installedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (SelectedPackagePair is { BasePackage.ExtensionManager: { } extensionManager } pair)
            {
                var manifestMap = await extensionManager.GetManifestExtensionsMapAsync(
                    extensionManager.GetManifests(pair.InstalledPackage)
                );
                manifestExtensions = manifestMap.Values.ToList();

                var installed = await extensionManager.GetInstalledExtensionsLiteAsync(pair.InstalledPackage);

                foreach (var extension in installed)
                {
                    if (extension.PrimaryPath?.Name is { } dirName)
                    {
                        installedNames.Add(dirName);
                    }

                    if (GetRepoName(extension.GitRepositoryUrl) is { } repoName)
                    {
                        installedNames.Add(repoName);
                    }
                }
            }

            Packs = new ObservableCollection<WorkflowNodePackViewModel>(
                requiredPacks
                    .Values.OrderBy(pack => pack.DisplayName)
                    .Select(pack =>
                    {
                        var extension = WorkflowNodeAnalyzer.MatchExtension(pack, manifestExtensions);
                        var isInstalled =
                            (pack.CnrId is { } cnrId && installedNames.Contains(cnrId))
                            || (
                                pack.AuxId?.Split('/').Last() is { } auxName
                                && installedNames.Contains(auxName)
                            )
                            || (
                                extension is { Reference: { } reference }
                                && GetRepoName(reference.ToString()) is { } extensionRepo
                                && installedNames.Contains(extensionRepo)
                            );

                        return new WorkflowNodePackViewModel
                        {
                            Pack = pack,
                            Extension = extension,
                            IsInstalled = isInstalled,
                            IsSelected = !isInstalled && extension is not null,
                        };
                    })
            );
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static string? GetRepoName(string? url) =>
        string.IsNullOrEmpty(url)
            ? null
            : url.TrimEnd('/').Split('/').LastOrDefault()?.Replace(".git", string.Empty);

    /// <summary>
    /// Shows the dialog for the given workflow files and runs the selected installs.
    /// </summary>
    public static async Task ShowDialogAsync(
        ISettingsManager settingsManager,
        IPackageFactory packageFactory,
        IReadOnlyList<FilePath> workflowFiles,
        bool onlyWhenActionable = false
    )
    {
        var vm = new WorkflowNodesDialogViewModel(settingsManager, packageFactory)
        {
            WorkflowFiles = workflowFiles,
        };

        if (onlyWhenActionable)
        {
            // Skip the dialog when there's nothing the user could do: everything is
            // already installed or can't be installed from the index anyway
            await vm.EnsureInitializedAsync();
            if (!vm.Packs.Any(pack => pack.CanInstall))
                return;
        }

        var dialog = new BetterContentDialog
        {
            IsPrimaryButtonEnabled = true,
            IsSecondaryButtonEnabled = true,
            PrimaryButtonText = Languages.Resources.Action_InstallSelected,
            SecondaryButtonText = Languages.Resources.Action_Close,
            DefaultButton = ContentDialogButton.Primary,
            IsFooterVisible = true,
            MaxDialogWidth = 600,
            MaxDialogHeight = 700,
            CloseOnClickOutside = true,
            Content = vm,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            return;

        var steps = vm.GetInstallSteps();
        if (steps.Count == 0)
            return;

        var runner = new PackageModificationRunner
        {
            ShowDialogOnStart = true,
            ModificationCompleteTitle = Languages.Resources.Progress_InstallationComplete,
            ModificationCompleteMessage = Languages.Resources.Progress_InstallationComplete,
        };
        EventManager.Instance.OnPackageInstallProgressAdded(runner);

        await runner.ExecuteSteps(steps);
    }
}

public partial class WorkflowNodePackViewModel : ObservableObject
{
    public required WorkflowNodePack Pack { get; init; }

    /// <summary>Matched extension index entry, null when the pack isn't in the index.</summary>
    public required PackageExtension? Extension { get; init; }

    public required bool IsInstalled { get; init; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public bool CanInstall => !IsInstalled && Extension is not null;

    public bool IsNotInIndex => !IsInstalled && Extension is null && !Pack.IsUnidentified;

    public string NodeTypesText => string.Join(", ", Pack.NodeTypes);
}
