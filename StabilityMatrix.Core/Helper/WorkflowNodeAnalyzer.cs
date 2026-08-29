using System.Text.Json.Nodes;
using StabilityMatrix.Core.Models.Packages.Extensions;

namespace StabilityMatrix.Core.Helper;

/// <summary>
/// Extracts the custom node packs a ComfyUI workflow file requires, from the
/// <c>cnr_id</c> / <c>aux_id</c> properties ComfyUI embeds per node when saving.
/// </summary>
public static class WorkflowNodeAnalyzer
{
    /// <summary>The registry id ComfyUI uses for its built-in nodes.</summary>
    private const string ComfyCoreId = "comfy-core";

    /// <summary>
    /// Returns the custom node packs referenced by the workflow, one entry per distinct pack.
    /// Nodes without pack information (workflows saved by older ComfyUI versions) are grouped
    /// into a single entry with <see cref="WorkflowNodePack.IsUnidentified"/> set.
    /// </summary>
    public static IReadOnlyList<WorkflowNodePack> GetRequiredPacks(JsonObject workflow)
    {
        if (workflow["nodes"] is not JsonArray nodes)
            return [];

        var packs = new Dictionary<string, WorkflowNodePack>(StringComparer.OrdinalIgnoreCase);
        var unidentifiedTypes = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in nodes.OfType<JsonObject>())
        {
            var nodeType = node["type"]?.GetValue<string>();
            var properties = node["properties"] as JsonObject;
            var cnrId = properties?["cnr_id"]?.GetValue<string>();
            var auxId = properties?["aux_id"]?.GetValue<string>();

            if (string.Equals(cnrId, ComfyCoreId, StringComparison.OrdinalIgnoreCase))
                continue;

            if (cnrId is null && auxId is null)
            {
                // Built-in node types are indistinguishable from custom ones here, so only
                // surface these when the workflow has no pack info at all (legacy format)
                if (nodeType is not null)
                {
                    unidentifiedTypes.Add(nodeType);
                }

                continue;
            }

            var key = cnrId ?? auxId!;
            if (!packs.TryGetValue(key, out var pack))
            {
                pack = new WorkflowNodePack { CnrId = cnrId, AuxId = auxId };
                packs[key] = pack;
            }

            if (nodeType is not null)
            {
                pack.NodeTypes.Add(nodeType);
            }
        }

        var result = packs.Values.ToList();

        // Only report unidentified nodes for legacy workflows - in a modern file every node
        // carries an id, so a missing one would just be noise next to real pack entries
        if (result.Count == 0 && unidentifiedTypes.Count > 0)
        {
            result.Add(new WorkflowNodePack { IsUnidentified = true, NodeTypes = [.. unidentifiedTypes] });
        }

        return result;
    }

    /// <summary>
    /// Finds the manifest extension for a pack: registry id match first, then repo/title
    /// name matches against <see cref="WorkflowNodePack.AuxId"/> and
    /// <see cref="WorkflowNodePack.CnrId"/>. Returns null when the pack isn't in the index.
    /// </summary>
    public static PackageExtension? MatchExtension(
        WorkflowNodePack pack,
        IReadOnlyCollection<PackageExtension> extensions
    )
    {
        if (pack.IsUnidentified)
            return null;

        if (pack.CnrId is { } cnrId)
        {
            if (
                extensions.FirstOrDefault(x =>
                    string.Equals(x.Id, cnrId, StringComparison.OrdinalIgnoreCase)
                ) is
                { } byId
            )
            {
                return byId;
            }
        }

        foreach (var name in new[] { pack.AuxId?.Split('/').Last(), pack.CnrId })
        {
            if (string.IsNullOrEmpty(name))
                continue;

            var match = extensions.FirstOrDefault(x =>
                string.Equals(GetRepoName(x.Reference), name, StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.Title, name, StringComparison.OrdinalIgnoreCase)
            );

            if (match is not null)
                return match;
        }

        return null;
    }

    private static string? GetRepoName(Uri? reference) =>
        reference?.ToString().TrimEnd('/').Split('/').LastOrDefault();
}

/// <summary>
/// A custom node pack referenced by a workflow file.
/// </summary>
public class WorkflowNodePack
{
    /// <summary>Comfy Node Registry id (e.g. "comfyui-kjnodes"), if the nodes carried one.</summary>
    public string? CnrId { get; init; }

    /// <summary>Fallback pack name ComfyUI records for non-registry installs, often the repo name.</summary>
    public string? AuxId { get; init; }

    /// <summary>
    /// True for the synthetic entry grouping nodes of a legacy workflow that carries
    /// no pack information at all.
    /// </summary>
    public bool IsUnidentified { get; init; }

    /// <summary>Distinct node types the workflow uses from this pack.</summary>
    public SortedSet<string> NodeTypes { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Best display name for the pack.</summary>
    public string DisplayName => AuxId?.Split('/').Last() ?? CnrId ?? "Unknown nodes";
}
