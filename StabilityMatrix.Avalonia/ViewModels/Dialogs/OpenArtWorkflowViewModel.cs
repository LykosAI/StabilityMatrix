using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;
using StabilityMatrix.Core.Models.Api.OpenArt;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

[View(typeof(OpenArtWorkflowDialog))]
[ManagedService]
[Transient]
public partial class OpenArtWorkflowViewModel : ContentDialogViewModelBase
{
    public required OpenArtSearchResult Workflow { get; init; }
    public string? InstalledComfyPath { get; init; }

    [ObservableProperty]
    private ObservableCollection<OpenArtCustomNode> customNodes = [];

    [ObservableProperty]
    private string prunedDescription = string.Empty;

    public override void OnLoaded()
    {
        CustomNodes = new ObservableCollection<OpenArtCustomNode>(ParseNodes(Workflow.NodesIndex.ToList()));
        PrunedDescription = Utilities.RemoveHtml(Workflow.Description);
    }

    [Localizable(false)]
    private List<OpenArtCustomNode> ParseNodes(List<string> nodes)
    {
        var indexOfFirstDot = nodes.IndexOf(".");
        if (indexOfFirstDot != -1)
        {
            nodes = nodes[(indexOfFirstDot + 1)..];
        }

        var installedNodes = new List<string>();
        if (!string.IsNullOrWhiteSpace(InstalledComfyPath))
        {
            installedNodes = Directory
                .EnumerateDirectories(InstalledComfyPath)
                .Select(
                    x => x.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries).Last()
                )
                .Where(x => ComfyNodeMap.Lookup.Values.FirstOrDefault(y => y.EndsWith(x)) != null)
                .ToList();
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
                    IsInstalled = installedNodes.Contains(node)
                };
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
