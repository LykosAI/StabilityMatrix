using Semver;

namespace StabilityMatrix.Core.Extensions;

public static class SemVersionExtensions
{
    public static string ToDisplayString(this SemVersion version)
    {
        var versionString = $"{version.Major}.{version.Minor}.{version.Patch}";

        // Add the build metadata if we have pre-release information
        if (version.PrereleaseIdentifiers.Count > 0)
        {
            versionString += $"-{version.Prerelease}";

            if (!string.IsNullOrWhiteSpace(version.Metadata))
            {
                // First 7 characters of the commit hash
                versionString += $"+{version.Metadata[..7]}";
            }
        }
        return versionString;
    }
}
