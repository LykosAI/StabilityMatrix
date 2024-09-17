using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using Semver;
using StabilityMatrix.Core.Processes;

namespace StabilityMatrix.Core.Models.Packages.Extensions;

/// <summary>
/// Extension specifier with optional version constraints.
/// </summary>
[PublicAPI]
public partial class ExtensionSpecifier
{
    public required string Name { get; init; }

    public string? Constraint { get; init; }

    public string? Version { get; init; }

    public string? VersionConstraint => Constraint is null || Version is null ? null : Constraint + Version;

    public bool TryGetSemVersionRange([NotNullWhen(true)] out SemVersionRange? semVersionRange)
    {
        if (!string.IsNullOrEmpty(VersionConstraint))
        {
            return SemVersionRange.TryParse(
                VersionConstraint,
                SemVersionRangeOptions.Loose,
                out semVersionRange
            );
        }

        semVersionRange = null;
        return false;
    }

    public static ExtensionSpecifier Parse(string value)
    {
        TryParse(value, true, out var packageSpecifier);

        return packageSpecifier!;
    }

    public static bool TryParse(string value, [NotNullWhen(true)] out ExtensionSpecifier? extensionSpecifier)
    {
        return TryParse(value, false, out extensionSpecifier);
    }

    private static bool TryParse(
        string value,
        bool throwOnFailure,
        [NotNullWhen(true)] out ExtensionSpecifier? packageSpecifier
    )
    {
        var match = ExtensionSpecifierRegex().Match(value);
        if (!match.Success)
        {
            if (throwOnFailure)
            {
                throw new ArgumentException($"Invalid extension specifier: {value}");
            }

            packageSpecifier = null;
            return false;
        }

        packageSpecifier = new ExtensionSpecifier
        {
            Name = match.Groups["extension_name"].Value,
            Constraint = match.Groups["version_constraint"].Value,
            Version = match.Groups["version"].Value
        };

        return true;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Name + VersionConstraint;
    }

    public Argument ToArgument()
    {
        if (VersionConstraint is not null)
        {
            // Use Name as key
            return new Argument(key: Name, value: ToString());
        }

        return new Argument(ToString());
    }

    public static implicit operator Argument(ExtensionSpecifier specifier)
    {
        return specifier.ToArgument();
    }

    public static implicit operator ExtensionSpecifier(string specifier)
    {
        return Parse(specifier);
    }

    /// <summary>
    /// Regex to match a pip package specifier.
    /// </summary>
    [GeneratedRegex(
        @"(?<extension_name>\S+)\s*(?<version_constraint>==|>=|<=|>|<|~=|!=)?\s*(?<version>[a-zA-Z0-9_.]+)?",
        RegexOptions.CultureInvariant,
        5000
    )]
    private static partial Regex ExtensionSpecifierRegex();
}
