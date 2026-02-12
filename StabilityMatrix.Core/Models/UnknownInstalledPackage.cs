using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Python;

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
            PythonVersion = PyInstallationManager.DefaultVersion.StringValue,
            LibraryPath = $"Packages{System.IO.Path.DirectorySeparatorChar}{name}",
        };
    }
}
