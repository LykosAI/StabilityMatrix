namespace StabilityMatrix.Core.Helper;

public static class EnumerationOptionConstants
{
    public static readonly EnumerationOptions TopLevelOnly =
        new() { RecurseSubdirectories = false, IgnoreInaccessible = true };

    public static readonly EnumerationOptions AllDirectories =
        new() { RecurseSubdirectories = true, IgnoreInaccessible = true };
}
