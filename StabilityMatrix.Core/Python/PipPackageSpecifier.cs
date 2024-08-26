using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using StabilityMatrix.Core.Processes;

namespace StabilityMatrix.Core.Python;

public partial record PipPackageSpecifier
{
    public string? Name { get; set; }

    public string? Constraint { get; set; }

    public string? Version { get; set; }

    public string? VersionConstraint => Constraint is null || Version is null ? null : Constraint + Version;

    public static PipPackageSpecifier Parse(string value)
    {
        var result = TryParse(value, true, out var packageSpecifier);

        Debug.Assert(result);

        return packageSpecifier!;
    }

    public static bool TryParse(string value, [NotNullWhen(true)] out PipPackageSpecifier? packageSpecifier)
    {
        return TryParse(value, false, out packageSpecifier);
    }

    private static bool TryParse(
        string value,
        bool throwOnFailure,
        [NotNullWhen(true)] out PipPackageSpecifier? packageSpecifier
    )
    {
        var match = PackageSpecifierRegex().Match(value);
        if (!match.Success)
        {
            if (throwOnFailure)
            {
                throw new ArgumentException($"Invalid package specifier: {value}");
            }

            packageSpecifier = null;
            return false;
        }

        packageSpecifier = new PipPackageSpecifier
        {
            Name = match.Groups["package_name"].Value,
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
        if (Name is null)
        {
            return new Argument("");
        }

        // Normal package specifier with version constraint
        if (VersionConstraint is not null)
        {
            // Use Name as key
            return new Argument(key: Name, value: ToString());
        }

        // Possible multi arg (e.g. '--extra-index-url ...')
        if (Name.Trim().StartsWith('-'))
        {
            var parts = Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 1)
            {
                var key = parts[0];
                var quotedParts = string.Join(' ', parts.Select(ProcessRunner.Quote));
                return Argument.Quoted(key, quotedParts);
            }
        }

        return new Argument(ToString());
    }

    public static implicit operator Argument(PipPackageSpecifier specifier)
    {
        return specifier.ToArgument();
    }

    public static implicit operator PipPackageSpecifier(string specifier)
    {
        return Parse(specifier);
    }

    /// <summary>
    /// Regex to match a pip package specifier.
    /// </summary>
    [GeneratedRegex(
        "(?<package_name>[a-zA-Z0-9_]+)(?<version_specifier>(?<version_constraint>==|>=|<=|>|<|~=|!=)(<version>[a-zA-Z0-9_.]+))?",
        RegexOptions.CultureInvariant,
        1000
    )]
    private static partial Regex PackageSpecifierRegex();
}
