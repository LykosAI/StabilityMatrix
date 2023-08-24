using StabilityMatrix.Core.Models.Packages;

namespace StabilityMatrix.Core.Models;

public class UnknownInstalledPackage : InstalledPackage
{
    public static UnknownInstalledPackage FromDirectoryName(string name)
    {
        return new UnknownInstalledPackage
        {
            Id = Guid.NewGuid(),
            PackageName = UnknownPackage.Key,
            DisplayName = name,
            LibraryPath = $"Packages{System.IO.Path.DirectorySeparatorChar}{name}",
        };
    }
}
