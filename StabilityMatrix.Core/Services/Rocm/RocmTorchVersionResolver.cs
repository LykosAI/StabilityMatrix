using System.Globalization;

namespace StabilityMatrix.Core.Services.Rocm;

/// <summary>
/// Resolves the ROCm torch/torchvision pairing. AMD's wheels declare no cross-constraints and the
/// matched build may be published only as a pre-release, so the pair is resolved from the index.
/// </summary>
internal static class RocmTorchVersionResolver
{
    // torch 2.x pairs with torchvision 0.(x + 15): 2.14 -> 0.29, 2.15 -> 0.30.
    private const int TorchvisionMinorOffset = 15;

    /// <summary>Strips a PEP 440 local label (for example <c>+rocm10.1.0</c>).</summary>
    public static string StripLocalVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return string.Empty;

        var plusIndex = version.IndexOf('+');
        return plusIndex < 0 ? version : version[..plusIndex];
    }

    /// <summary>
    /// Returns the <c>(major, minor)</c> release pair, ignoring pre-release/dev/post segments and the
    /// local label. Returns <see langword="null"/> when the release segment cannot be parsed.
    /// </summary>
    public static (int Major, int Minor)? TryGetReleasePair(string? version)
    {
        var core = StripLocalVersion(version);
        if (string.IsNullOrWhiteSpace(core))
            return null;

        // Cut at the first character outside the numeric release (the "a0" in "0.29.0a0").
        var end = 0;
        while (end < core.Length && (char.IsDigit(core[end]) || core[end] == '.'))
        {
            end++;
        }

        var parts = core[..end].TrimEnd('.').Split('.');
        if (
            parts.Length < 2
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var major)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minor)
        )
        {
            return null;
        }

        return (major, minor);
    }

    /// <summary>Returns <see langword="true"/> for development releases (for example <c>2.15.0.dev20261006</c>).</summary>
    public static bool IsDevelopmentRelease(string? version) =>
        StripLocalVersion(version).Contains("dev", StringComparison.OrdinalIgnoreCase);

    /// <summary>Derives the torchvision <c>(major, minor)</c> paired with a torch version, or <see langword="null"/> if unknown.</summary>
    public static (int Major, int Minor)? ExpectedTorchvision(string? torchVersion)
    {
        if (TryGetReleasePair(torchVersion) is not { } torch)
            return null;

        // Only the torch 2.x line shares the torchvision 0.x versioning used by the AMD index.
        if (torch.Major != 2)
            return null;

        return (0, torch.Minor + TorchvisionMinorOffset);
    }

    /// <summary>
    /// Selects the highest version whose release pair is <paramref name="expected"/> (input is in pip's
    /// descending order), skipping development releases.
    /// </summary>
    public static string? SelectHighestMatching(
        IReadOnlyList<string>? availableVersions,
        (int Major, int Minor) expected
    )
    {
        if (availableVersions is null)
            return null;

        foreach (var version in availableVersions)
        {
            if (!IsDevelopmentRelease(version) && TryGetReleasePair(version) == expected)
                return StripLocalVersion(version);
        }

        return null;
    }

    /// <summary>
    /// Yields the distinct base versions (local labels stripped) in their original order, so local-label
    /// variants (<c>2.14.0+rocm10.0.0</c> / <c>2.14.0+rocm10.1.0</c>) aren't walked twice.
    /// </summary>
    public static IEnumerable<string> DistinctByBaseVersion(IReadOnlyList<string>? versions)
    {
        if (versions is null)
            yield break;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var version in versions)
        {
            var baseVersion = StripLocalVersion(version);
            if (!string.IsNullOrWhiteSpace(baseVersion) && seen.Add(baseVersion))
                yield return version;
        }
    }

    /// <summary>
    /// Selects the newest torch that has a paired torchvision build, walking down until one matches.
    /// Returns the paired base versions (local labels stripped), or <see langword="null"/> if none pair.
    /// </summary>
    public static (string Torch, string Torchvision)? SelectHighestPair(
        IReadOnlyList<string>? torchVersions,
        IReadOnlyList<string>? torchvisionVersions
    )
    {
        if (torchVersions is null || torchvisionVersions is null)
            return null;

        foreach (var torchVersion in DistinctByBaseVersion(torchVersions))
        {
            if (ExpectedTorchvision(torchVersion) is not { } expected)
                continue;

            if (SelectHighestMatching(torchvisionVersions, expected) is { } torchvisionVersion)
                return (StripLocalVersion(torchVersion), torchvisionVersion);
        }

        return null;
    }

    /// <summary>Returns the local version label (for example <c>+rocm10.2.0a20261007</c>), or empty.</summary>
    public static string GetLocalVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return string.Empty;

        var plusIndex = version.IndexOf('+');
        return plusIndex < 0 ? string.Empty : version[plusIndex..];
    }

    /// <summary>
    /// Selects the newest torch build that has a paired torchvision build carrying the same local label
    /// (for example <c>+rocm10.2.0a20261007</c>), so both are pinned to the same dated snapshot. Returns
    /// the full versions (local labels included), or <see langword="null"/> when no snapshot pairs.
    /// </summary>
    public static (string Torch, string Torchvision)? SelectHighestPairBySnapshot(
        IReadOnlyList<string>? torchVersions,
        IReadOnlyList<string>? torchvisionVersions
    )
    {
        if (torchVersions is null || torchvisionVersions is null)
            return null;

        // torchVersions is in pip's descending order, so the first matching snapshot is the newest.
        foreach (var torchVersion in torchVersions)
        {
            if (IsDevelopmentRelease(torchVersion) || ExpectedTorchvision(torchVersion) is not { } expected)
                continue;

            var localVersion = GetLocalVersion(torchVersion);
            if (string.IsNullOrWhiteSpace(localVersion))
                continue;

            foreach (var torchvisionVersion in torchvisionVersions)
            {
                if (IsDevelopmentRelease(torchvisionVersion))
                    continue;

                if (
                    TryGetReleasePair(torchvisionVersion) == expected
                    && string.Equals(
                        GetLocalVersion(torchvisionVersion),
                        localVersion,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    return (torchVersion, torchvisionVersion);
                }
            }
        }

        return null;
    }
}
