namespace StabilityMatrix.Core.Models.Packages.Extensions;

/// <summary>
/// Matches required <see cref="ExtensionSpecifier"/>s against the extensions found on disk.
/// </summary>
/// <remarks>
/// Extensions can reach a package by paths other than our own git clone - ComfyUI-Manager's
/// registry installs unpack a zip into a lowercased folder with no git metadata at all, and
/// hand-cloned repositories carry equivalent-but-not-identical remote urls (<c>.git</c> suffix,
/// trailing slash, ssh remote). Comparing raw urls treats all of those as "not installed".
/// </remarks>
public static class ExtensionMatcher
{
    /// <summary>
    /// Returns the installed extension satisfying the specifier, or null if none does.
    /// </summary>
    public static InstalledPackageExtension? FindInstalled(
        ExtensionSpecifier specifier,
        IEnumerable<InstalledPackageExtension> installedExtensions
    )
    {
        var requiredUrl = NormalizeRepositoryUrl(specifier.Name);
        var requiredFolderName = GetRepositoryName(requiredUrl);

        InstalledPackageExtension? folderNameMatch = null;

        foreach (var installed in installedExtensions)
        {
            if (
                requiredUrl is not null
                && NormalizeRepositoryUrl(installed.GitRepositoryUrl) is { } installedUrl
                && installedUrl.Equals(requiredUrl, StringComparison.OrdinalIgnoreCase)
            )
            {
                return installed;
            }

            if (
                requiredFolderName is not null
                && installed.PrimaryPath?.Name is { } folderName
                && folderName.Equals(requiredFolderName, StringComparison.OrdinalIgnoreCase)
            )
            {
                // Keep looking - a remote url match is the stronger signal
                folderNameMatch ??= installed;
            }
        }

        return folderNameMatch;
    }

    /// <summary>
    /// Returns the specifiers that no installed extension satisfies.
    /// </summary>
    public static List<ExtensionSpecifier> GetMissing(
        IEnumerable<ExtensionSpecifier> requiredExtensions,
        IEnumerable<InstalledPackageExtension> installedExtensions
    )
    {
        var installed =
            installedExtensions as IReadOnlyList<InstalledPackageExtension> ?? installedExtensions.ToList();

        return requiredExtensions.Where(specifier => FindInstalled(specifier, installed) is null).ToList();
    }

    /// <summary>
    /// Reduces a git remote url to a comparable <c>host/owner/repo</c> form, or null if
    /// <paramref name="url"/> is null or empty.
    /// </summary>
    public static string? NormalizeRepositoryUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var normalized = url.Trim();

        // scp-style ssh remote: git@github.com:owner/repo(.git)
        if (!normalized.Contains("://", StringComparison.Ordinal) && normalized.Contains(':'))
        {
            normalized = normalized.Replace(':', '/');
        }
        else if (normalized.IndexOf("://", StringComparison.Ordinal) is var schemeEnd and >= 0)
        {
            normalized = normalized[(schemeEnd + 3)..];
        }

        // Credentials or ssh user, e.g. https://token@github.com/owner/repo - only in the host part
        var hostEnd = normalized.IndexOf('/');
        var atIndex = normalized.IndexOf('@');
        if (atIndex >= 0 && (hostEnd < 0 || atIndex < hostEnd))
        {
            normalized = normalized[(atIndex + 1)..];
        }

        normalized = normalized.TrimEnd('/');

        if (normalized.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^4];
        }

        if (normalized.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[4..];
        }

        return normalized.Length == 0 ? null : normalized;
    }

    /// <summary>
    /// Returns the repository name (last url segment), which is also the folder name a clone
    /// of it lands in, or null if there is none.
    /// </summary>
    private static string? GetRepositoryName(string? normalizedUrl)
    {
        if (normalizedUrl is null)
        {
            return null;
        }

        var lastSegment = normalizedUrl[(normalizedUrl.LastIndexOf('/') + 1)..];

        return lastSegment.Length == 0 ? null : lastSegment;
    }
}
