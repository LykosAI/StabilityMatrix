using System;
using System.IO;
using System.Runtime.Versioning;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;

namespace StabilityMatrix.Core.Python;

/// <summary>
/// Represents a specific Python installation
/// </summary>
public class PyInstallation
{
    /// <summary>
    /// The version of this Python installation
    /// </summary>
    public PyVersion Version { get; }

    /// <summary>
    /// The root directory of this Python installation
    /// </summary>
    public DirectoryPath RootDir { get; }

    /// <summary>
    /// The name of the Python directory
    /// </summary>
    public string DirectoryName =>
        Version == PyInstallationManager.Python_3_10_11
            ? "Python310"
            : $"Python{Version.Major}{Version.Minor}{Version.Micro}";

    /// <summary>
    /// Path to the Python installation directory
    /// </summary>
    public string InstallPath => Path.Combine(GlobalConfig.LibraryDir, "Assets", DirectoryName);

    /// <summary>
    /// Path to the Python linked library relative from the Python directory
    /// </summary>
    public string RelativePythonDllPath =>
        Compat.Switch(
            (PlatformKind.Windows, $"python{Version.Major}{Version.Minor}.dll"),
            (PlatformKind.Linux, Path.Combine("lib", $"libpython{Version.Major}.{Version.Minor}.so")),
            (PlatformKind.MacOS, Path.Combine("lib", $"libpython{Version.Major}.{Version.Minor}.dylib"))
        );

    /// <summary>
    /// Full path to the Python linked library
    /// </summary>
    public string PythonDllPath => Path.Combine(InstallPath, RelativePythonDllPath);

    /// <summary>
    /// Path to the Python executable
    /// </summary>
    public string PythonExePath =>
        Compat.Switch(
            (PlatformKind.Windows, Path.Combine(InstallPath, "python.exe")),
            (PlatformKind.Linux, Path.Combine(InstallPath, "bin", "python3")),
            (PlatformKind.MacOS, Path.Combine(InstallPath, "bin", "python3"))
        );

    /// <summary>
    /// Path to the pip executable
    /// </summary>
    public string PipExePath =>
        Compat.Switch(
            (PlatformKind.Windows, Path.Combine(InstallPath, "Scripts", "pip.exe")),
            (PlatformKind.Linux, Path.Combine(InstallPath, "bin", "pip3")),
            (PlatformKind.MacOS, Path.Combine(InstallPath, "bin", "pip3"))
        );

    /// <summary>
    /// Path to the get-pip script
    /// </summary>
    public string GetPipPath => Path.Combine(InstallPath, "get-pip.pyc");

    /// <summary>
    /// Path to the virtualenv executable
    /// </summary>
    public string VenvPath => Path.Combine(InstallPath, "Scripts", "virtualenv" + Compat.ExeExtension);

    /// <summary>
    /// Check if pip is installed
    /// </summary>
    public bool PipInstalled => File.Exists(PipExePath);

    /// <summary>
    /// Check if virtualenv is installed
    /// </summary>
    public bool VenvInstalled => File.Exists(VenvPath);

    /// <summary>
    /// Construct a Python installation
    /// </summary>
    public PyInstallation(PyVersion version)
    {
        Version = version;
        RootDir = new DirectoryPath(InstallPath);
    }

    /// <summary>
    /// Construct a Python installation with a specific major and minor version
    /// </summary>
    public PyInstallation(int major, int minor, int micro = 0)
        : this(new PyVersion(major, minor, micro)) { }

    /// <summary>
    /// Check if this Python installation exists
    /// </summary>
    public bool Exists() => File.Exists(PythonDllPath);

    /// <summary>
    /// Creates a unique identifier for this Python installation
    /// </summary>
    public override string ToString() => $"Python {Version}";
}
