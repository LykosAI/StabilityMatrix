namespace StabilityMatrix.Models.Packages;

/// <summary>
/// Supports parsing launch options from a python script.
/// </summary>
public interface IArgParsable
{
    /// <summary>
    /// Defines the relative path to the python script that defines the launch options.
    /// </summary>
    public string RelativeArgsDefinitionScriptPath { get; }
}
