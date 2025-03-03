namespace StabilityMatrix.Core.Models;

public class ExtraPackageCommand
{
    public required string CommandName { get; set; }
    public required Func<InstalledPackage, Task> Command { get; set; }
}
