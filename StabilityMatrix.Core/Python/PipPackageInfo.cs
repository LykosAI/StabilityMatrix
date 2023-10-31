namespace StabilityMatrix.Core.Python;

public readonly record struct PipPackageInfo(
    string Name,
    string Version,
    string? EditableProjectLocation = null
);
