namespace StabilityMatrix.Core.Models.Packages.Extensions;

public record PackageExtensionVersion : GitVersion
{
    public override string ToString() => base.ToString();
};
