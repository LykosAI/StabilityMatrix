namespace StabilityMatrix.Core.Helper;

public static class EnumerationOptionConstants
{
    public static readonly EnumerationOptions TopLevelOnly = new()
    {
        RecurseSubdirectories = false,
        IgnoreInaccessible = true,
    };

    public static readonly EnumerationOptions AllDirectories = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
    };

    /// <summary>
    /// Recurses into subdirectories but never through a symbolic link or junction, at any depth,
    /// and includes hidden and system files. Use it to measure what a recursive delete removes.
    /// </summary>
    public static readonly EnumerationOptions AllDirectoriesExceptLinks = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
    };
}
