using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using StabilityMatrix.Core.Processes;

namespace StabilityMatrix.Core.Python;

public partial record UvPackageSpecifier
{
    [JsonIgnore]
    public static IReadOnlyList<string> ConstraintOptions => ["", "==", "~=", ">=", "<=", ">", "<"];

    public string? Name { get; set; }

    public string? Constraint { get; set; }

    public string? Version { get; set; }

    public string? VersionConstraint => Constraint is null || Version is null ? null : Constraint + Version;

    public static UvPackageSpecifier Parse(string value)
    {
        var result = TryParse(value, true, out var packageSpecifier);

        Debug.Assert(result);

        return packageSpecifier!;
    }

    public static bool TryParse(string value, [NotNullWhen(true)] out UvPackageSpecifier? packageSpecifier)
    {
        return TryParse(value, false, out packageSpecifier);
    }

    private static bool TryParse(
        string value,
        bool throwOnFailure,
        [NotNullWhen(true)] out UvPackageSpecifier? packageSpecifier
    )
    {
        // uv allows for more complex specifiers, including URLs and path specifiers directly.
        // For now, this regex focuses on PyPI-style name and version specifiers.
        // Enhancements could be made here to support git URLs, local paths, etc. if needed.
        var match = PackageSpecifierRegex().Match(value);
        if (!match.Success)
        {
            // Check if it's a URL or path-like specifier (basic check)
            // uv supports these directly. For simplicity, we'll treat them as a Name-only specifier for now.
            if (
                Uri.IsWellFormedUriString(value, UriKind.Absolute)
                || value.Contains(Path.DirectorySeparatorChar)
                || value.Contains(Path.AltDirectorySeparatorChar)
            )
            {
                packageSpecifier = new UvPackageSpecifier { Name = value };
                return true;
            }

            if (throwOnFailure)
            {
                throw new ArgumentException($"Invalid or unsupported package specifier for uv: {value}");
            }

            packageSpecifier = null;
            return false;
        }

        packageSpecifier = new UvPackageSpecifier
        {
            Name = match.Groups["package_name"].Value,
            Constraint = match.Groups["version_constraint"].Value, // Will be empty string if no constraint
            Version = match.Groups["version"].Value // Will be empty string if no version
        };

        // Ensure Constraint and Version are null if they were empty strings from regex.
        if (string.IsNullOrEmpty(packageSpecifier.Constraint))
            packageSpecifier.Constraint = null;
        if (string.IsNullOrEmpty(packageSpecifier.Version))
            packageSpecifier.Version = null;

        return true;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        if (Name is null)
            return string.Empty;
        return Name + (VersionConstraint ?? string.Empty);
    }

    public Argument ToArgument()
    {
        if (Name is null)
        {
            return new Argument("");
        }

        // Handle URL or path specifiers - they are typically just the value itself.
        if (
            Uri.IsWellFormedUriString(Name, UriKind.Absolute)
            || Name.Contains(Path.DirectorySeparatorChar)
            || Name.Contains(Path.AltDirectorySeparatorChar)
        )
        {
            return new Argument(ProcessRunner.Quote(Name)); // Ensure paths with spaces are quoted
        }

        // Normal package specifier with optional version constraint
        if (VersionConstraint is not null)
        {
            // Use Name as key to allow for potential overrides if the builder uses keys
            // Otherwise, it's just value. For uv install, it's usually just the full string "package==version".
            return new Argument(key: Name, value: ToString());
        }

        // Handles cases like "--extra-index-url <url>" or other flags passed as package names.
        // This logic might be more relevant for a generic ArgsBuilder than for a package specifier directly,
        // unless these are passed in a requirements file and parsed this way.
        if (Name.Trim().StartsWith('-'))
        {
            var parts = Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 1)
            {
                var key = parts[0];
                // Re-join parts, quoting each if necessary (though Name should be the first part here)
                // This specific case might be better handled by the ArgsBuilder itself.
                // For a UvPackageSpecifier, if Name starts with '-', it's usually a single argument value (e.g. from req.txt).
                return Argument.Quoted(key, Name); // Or simply new Argument(Name) if it's a single directive
            }
        }

        return new Argument(ToString());
    }

    public static implicit operator Argument(UvPackageSpecifier specifier)
    {
        return specifier.ToArgument();
    }

    public static implicit operator UvPackageSpecifier(string specifier)
    {
        return Parse(specifier);
    }

    /// <summary>
    /// Regex to match a pip/uv package specifier with name and optional version.
    /// Does not explicitly match URLs or file paths, those are handled as a fallback.
    /// (?i) for case-insensitive package names, though PyPI is case-insensitive in practice.
    /// </summary>
    [GeneratedRegex(
        @"^(?<package_name>[a-zA-Z0-9_.-]+)(?:(?<version_constraint>[~><=!]=?|[><])\s*(?<version>[a-zA-Z0-9_.*+-]+))?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled
    )]
    private static partial Regex PackageSpecifierRegex();
}
