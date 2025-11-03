using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Factory;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.OpenArt;
using StabilityMatrix.Core.Models.Packages.Extensions;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

[View(typeof(OpenArtWorkflowDialog))]
[ManagedService]
[RegisterTransient<OpenArtWorkflowViewModel>]
public partial class OpenArtWorkflowViewModel(
    ISettingsManager settingsManager,
    IPackageFactory packageFactory
) : ContentDialogViewModelBase
{
    public required OpenArtSearchResult Workflow { get; init; }

    [ObservableProperty]
    private ObservableCollection<OpenArtCustomNode> customNodes = [];

    [ObservableProperty]
    private string prunedDescription = string.Empty;

    [ObservableProperty]
    private bool installRequiredNodes = true;

    [ObservableProperty]
    private InstalledPackage? selectedPackage;

    public PackagePair? SelectedPackagePair =>
        SelectedPackage is { } package ? packageFactory.GetPackagePair(package) : null;

    public List<InstalledPackage> AvailablePackages =>
        settingsManager
            .Settings.InstalledPackages.Where(package => package.PackageName is "ComfyUI" or "ComfyUI-Zluda")
            .ToList();

    public List<PackageExtension> MissingNodes { get; } = [];

    public override async Task OnLoadedAsync()
    {
        if (Design.IsDesignMode)
            return;

        if (settingsManager.Settings.PreferredWorkflowPackage is { } preferredPackage)
        {
            SelectedPackage = preferredPackage;
        }
        else
        {
            SelectedPackage = AvailablePackages.FirstOrDefault();
        }

        if (SelectedPackage == null)
        {
            InstallRequiredNodes = false;
        }

        CustomNodes = new ObservableCollection<OpenArtCustomNode>(
            await ParseNodes(Workflow.NodesIndex.ToList())
        );
        PrunedDescription = Utilities.RemoveHtml(Workflow.Description);
    }

    partial void OnSelectedPackageChanged(InstalledPackage? oldValue, InstalledPackage? newValue)
    {
        if (oldValue is null)
            return;

        settingsManager.Transaction(settings =>
        {
            settings.PreferredWorkflowPackage = newValue;
        });

        OnLoadedAsync().SafeFireAndForget();
    }

    [Localizable(false)]
    private async Task<List<OpenArtCustomNode>> ParseNodes(List<string> nodes)
    {
        var indexOfFirstDot = nodes.IndexOf(".");
        if (indexOfFirstDot != -1)
        {
            nodes = nodes[(indexOfFirstDot + 1)..];
        }

        var installedNodesNames = new HashSet<string>();
        var nameToManifestNodes = new Dictionary<string, PackageExtension>();
        var addedMissingNodes = new HashSet<string>();

        var packagePair = SelectedPackagePair;

        if (packagePair?.BasePackage.ExtensionManager is { } extensionManager)
        {
            var installedNodes = (
                await extensionManager.GetInstalledExtensionsLiteAsync(packagePair.InstalledPackage)
            ).ToList();

            var manifestExtensionsMap = await extensionManager.GetManifestExtensionsMapAsync(
                extensionManager.GetManifests(packagePair.InstalledPackage)
            );

            // Add manifestExtensions definition to installedNodes if matching git repository url
            installedNodes = installedNodes
                .Select(installedNode =>
                {
                    if (
                        installedNode.GitRepositoryUrl is not null
                        && manifestExtensionsMap.TryGetValue(
                            installedNode.GitRepositoryUrl,
                            out var manifestExtension
                        )
                    )
                    {
                        installedNode = installedNode with { Definition = manifestExtension };
                    }

                    return installedNode;
                })
                .ToList();

            // There may be duplicate titles, deduplicate by using the first one
            nameToManifestNodes = manifestExtensionsMap
                .GroupBy(x => x.Value.Title)
                .ToDictionary(x => x.Key, x => x.First().Value);

            installedNodesNames = installedNodes.Select(x => x.Title).ToHashSet();
        }

        var sections = new List<OpenArtCustomNode>();
        OpenArtCustomNode? currentSection = null;

        foreach (var node in nodes)
        {
            if (node is "." or ",")
            {
                currentSection = null; // End of the current section
                continue;
            }

            if (currentSection == null)
            {
                currentSection = new OpenArtCustomNode
                {
                    Title = node,
                    IsInstalled = installedNodesNames.Contains(node),
                };

                // Add missing nodes to the list (deduplicate by title)
                if (
                    !currentSection.IsInstalled
                    && nameToManifestNodes.TryGetValue(node, out var manifestNode)
                    && addedMissingNodes.Add(manifestNode.Title)
                )
                {
                    MissingNodes.Add(manifestNode);
                }

                sections.Add(currentSection);
            }
            else
            {
                currentSection.Children.Add(node);
            }
        }

        if (sections.FirstOrDefault(x => x.Title == "ComfyUI") != null)
        {
            sections = sections.Where(x => x.Title != "ComfyUI").ToList();
        }

        return sections;
    }
}
