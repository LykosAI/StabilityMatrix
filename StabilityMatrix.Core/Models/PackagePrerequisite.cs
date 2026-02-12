namespace StabilityMatrix.Core.Models;

public enum PackagePrerequisite
{
    Python310,
    Python31017,

    /// <summary>
    /// Python managed via UV - version determined by package's RecommendedPythonVersion.
    /// This is the preferred prerequisite for new packages, replacing Python310.
    /// </summary>
    PythonUvManaged,
    VcRedist,
    Git,
    HipSdk,
    Node,
    Dotnet,
    Tkinter,
    VcBuildTools
}
