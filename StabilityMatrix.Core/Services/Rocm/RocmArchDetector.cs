using NLog;

namespace StabilityMatrix.Core.Services.Rocm;

/// <summary>
/// Reads the ISA targets the amdgpu kernel module exports through the KFD topology. On Linux this is the
/// driver's own answer, so no ROCm userspace and no marketing-name table is involved. Windows has no KFD
/// node and keeps resolving architectures from the adapter name table.
/// </summary>
internal static class RocmArchDetector
{
    private const string KfdNodesPath = "/sys/class/kfd/kfd/topology/nodes";

    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private static readonly object Gate = new();
    private static IReadOnlyList<string>? cached;

    /// <summary>ISA targets the driver reports, in node order. Empty when KFD is unavailable.</summary>
    internal static IReadOnlyList<string> Architectures
    {
        get
        {
            if (cached is { } existing)
            {
                return existing;
            }

            lock (Gate)
            {
                return cached ??= Detect();
            }
        }
    }

    /// <summary>Forces the next access to re-read the topology.</summary>
    internal static void ResetCache()
    {
        lock (Gate)
        {
            cached = null;
        }
    }

    private static IReadOnlyList<string> Detect()
    {
        try
        {
            if (!OperatingSystem.IsLinux() || !Directory.Exists(KfdNodesPath))
            {
                return [];
            }

            List<string> architectures = [];

            foreach (var nodePath in Directory.EnumerateDirectories(KfdNodesPath))
            {
                var propertiesPath = Path.Combine(nodePath, "properties");
                if (!File.Exists(propertiesPath))
                {
                    continue;
                }

                var architecture = TryParseNode(File.ReadAllText(propertiesPath));
                if (architecture is not null)
                {
                    architectures.Add(architecture);
                }
            }

            return architectures;
        }
        catch (Exception exception)
        {
            Logger.Warn(exception, "Failed to read the KFD topology; falling back to adapter names.");
            return [];
        }
    }

    /// <summary>
    /// Decodes one node's properties text, or returns null for CPU nodes and unparsable nodes.
    /// </summary>
    internal static string? TryParseNode(string properties)
    {
        Dictionary<string, int> values = new(StringComparer.Ordinal);

        foreach (var line in properties.Split('\n'))
        {
            var parts = line.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            );
            if (parts.Length == 2 && int.TryParse(parts[1], out var value))
            {
                values[parts[0]] = value;
            }
        }

        // CPU nodes carry simd_count 0 and no target version.
        if (!values.TryGetValue("simd_count", out var simdCount) || simdCount == 0)
        {
            return null;
        }

        if (!values.TryGetValue("gfx_target_version", out var targetVersion) || targetVersion == 0)
        {
            return null;
        }

        return FormatGfxTarget(targetVersion);
    }

    /// <summary>
    /// Decodes the KFD <c>gfx_target_version</c> integer into its ISA name: 110000 to gfx1100,
    /// 120001 to gfx1201, 100300 to gfx1030, 110500 to gfx1150, 90010 to gfx90a. The name is always
    /// major, then minor, then the stepping as a single hex digit, including when the stepping is zero.
    /// </summary>
    internal static string FormatGfxTarget(int gfxTargetVersion)
    {
        var major = gfxTargetVersion / 10000;
        var minor = gfxTargetVersion / 100 % 100;
        var stepping = gfxTargetVersion % 100;

        return $"gfx{major}{minor}{stepping:x}";
    }
}
