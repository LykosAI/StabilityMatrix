using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace StabilityMatrix.Core.Models.Documentation;

/// <summary>
/// The copy of the docs tree embedded in the app at build time. Read only when the live copy
/// is unreachable — the network copy stays authoritative so pages added after a release still
/// appear — but it guarantees the viewer is never empty, including on a first run that is
/// offline or has exhausted the anonymous GitHub API budget.
/// </summary>
public static class BundledDocumentation
{
    private const string ResourcePrefix = "BundledDocs/";

    private static readonly Lazy<IReadOnlyDictionary<string, string>> ResourcesByPath = new(BuildIndex);

    /// <summary>
    /// Docs-relative paths present in the bundle, e.g. <c>getting-started/overview.md</c>.
    /// Empty if the build produced no snapshot.
    /// </summary>
    public static IReadOnlyList<string> Paths => ResourcesByPath.Value.Keys.ToList();

    /// <summary>
    /// Reads a bundled page, or null when the path is not part of the snapshot.
    /// </summary>
    public static string? TryReadPage(string docsRelativePath)
    {
        if (!ResourcesByPath.Value.TryGetValue(docsRelativePath, out var resourceName))
            return null;

        using var stream = typeof(BundledDocumentation).Assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
            return null;

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static IReadOnlyDictionary<string, string> BuildIndex()
    {
        var assembly = typeof(BundledDocumentation).Assembly;

        return assembly
            .GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            .ToDictionary(
                name => name[ResourcePrefix.Length..],
                name => name,
                StringComparer.OrdinalIgnoreCase
            );
    }
}
