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
            PythonVersion = PyInstallationManager.Python_3_10_16.StringValue,
            LibraryPath = $"Packages{System.IO.Path.DirectorySeparatorChar}{name}",
        };
    }
}
