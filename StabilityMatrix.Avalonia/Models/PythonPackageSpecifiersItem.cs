using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Core.Python;

namespace StabilityMatrix.Avalonia.Models;

/// <summary>
/// Row item of <see cref="PythonPackageSpecifiersViewModel"/>.
/// </summary>
public class PythonPackageSpecifiersItem
{
    public string? Name { get; set; }
    public string? Constraint { get; set; }
    public string? Version { get; set; }
    public PipPackageSpecifierOverrideAction Action { get; set; }

    public static PythonPackageSpecifiersItem FromSpecifier(PipPackageSpecifierOverride specifier)
    {
        return new PythonPackageSpecifiersItem
        {
            Name = specifier.Name,
            Constraint = specifier.Constraint,
            Version = specifier.Version,
            Action = specifier.Action
        };
    }

    public PipPackageSpecifierOverride ToSpecifier()
    {
        return new PipPackageSpecifierOverride
        {
            Name = Name,
            Constraint = Constraint,
            Version = Version,
            Action = Action
        };
    }
}
