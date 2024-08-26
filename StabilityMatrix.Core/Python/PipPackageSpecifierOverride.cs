using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Python;

public record PipPackageSpecifierOverride : PipPackageSpecifier
{
    public PipPackageSpecifierOverrideAction Action { get; init; } = PipPackageSpecifierOverrideAction.Update;

    [JsonIgnore]
    public bool IsUpdate => Action is PipPackageSpecifierOverrideAction.Update;
}
