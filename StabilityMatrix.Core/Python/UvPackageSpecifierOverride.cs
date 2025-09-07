using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Python;

public record UvPackageSpecifierOverride : UvPackageSpecifier
{
    public UvPackageSpecifierOverrideAction Action { get; init; } = UvPackageSpecifierOverrideAction.Update;

    [JsonIgnore]
    public bool IsUpdate => Action is UvPackageSpecifierOverrideAction.Update;

    /// <inheritdoc />
    public override string ToString()
    {
        // The base ToString() from UvPackageSpecifier should be sufficient as it already formats
        // the package name and version constraint (e.g., "package_name==1.0.0").
        // The Action property influences how this specifier is used by an ArgsBuilder,
        // rather than its string representation as a package.
        return base.ToString();
    }
}
