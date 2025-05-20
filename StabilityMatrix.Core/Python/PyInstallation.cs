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
    /// The root directory of this Python installation.
    /// This is the primary source of truth for the installation's location.
    /// </summary>
    public DirectoryPath RootDir { get; }

    /// <summary>
    /// Path to the Python installation directory.
    /// Derived from RootDir.
    /// </summary>
    public string InstallPath => RootDir.FullPath;

    /// <summary>
    /// The name of the Python directory (e.g., "Python310", "Python31011")
    /// This is more of a convention for legacy paths or naming.
    /// If RootDir is arbitrary (e.g., from UV default), this might just be RootDir.Name.
    /// </summary>
    public string DirectoryName
    {
        get
        {
            // If the RootDir seems to follow our old convention, use the old logic.
            // Otherwise, just use the directory name from RootDir.
            var expectedLegacyDirName = GetDirectoryNameForVersion(Version);
            if (Version == PyInstallationManager.Python_3_10_11) // Special case from original
            {
                expectedLegacyDirName = "Python310";
            }

            if (
                RootDir.Name.Equals(expectedLegacyDirName, StringComparison.OrdinalIgnoreCase)
                || (
                    Version == PyInstallationManager.Python_3_10_11
                    && RootDir.Name.Equals("Python310", StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                return RootDir.Name; // It matches a known pattern or is the direct name
            }
            // If InstallPath was calculated by the old logic, RootDir.Name would be the DirectoryName.
            // If InstallPath was provided directly (e.g. UV default path), then RootDir.Name is just the last segment of that path.
            return RootDir.Name;
        }
    }

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
            (PlatformKind.Linux, Path.Combine(InstallPath, "bin", "python3")), // Could also be 'python' if uv installs it that way or it's a system python
            (PlatformKind.MacOS, Path.Combine(InstallPath, "bin", "python3")) // Same as Linux
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

    // These might become less relevant if UV handles venv creation and pip directly for venvs
    // but the base Python installation will still have them.
    /// <summary>
    /// Path to the get-pip script (less relevant with UV)
    /// </summary>
    public string GetPipPath => Path.Combine(InstallPath, "get-pip.pyc"); // This path is specific, might not exist in UV installs

    /// <summary>
    /// Path to the virtualenv executable (less relevant with UV)
    /// </summary>
    public string VenvPath => Path.Combine(InstallPath, "Scripts", "virtualenv" + Compat.ExeExtension);

    /// <summary>
    /// Check if pip is installed in this base Python.
    /// </summary>
    public bool PipInstalled => File.Exists(PipExePath);

    /// <summary>
    /// Check if virtualenv is installed (less relevant with UV).
    /// </summary>
    public bool VenvInstalled => File.Exists(VenvPath);

    public bool UsesUv => Version != PyInstallationManager.Python_3_10_11;

    /// <summary>
    /// Primary constructor for when the installation path is known.
    /// This should be used by PyInstallationManager when it discovers an installation (legacy or UV-managed).
    /// </summary>
    /// <param name="version">The Python version.</param>
    /// <param name="installPath">The full path to the root of the Python installation.</param>
    public PyInstallation(PyVersion version, string installPath)
    {
        Version = version;
        RootDir = new DirectoryPath(installPath); // Set RootDir directly

        // Basic validation: ensure the path is not empty. More checks could be added.
        if (string.IsNullOrWhiteSpace(installPath))
        {
            throw new ArgumentException("Installation path cannot be null or empty.", nameof(installPath));
        }
    }

    /// <summary>
    /// Constructor for legacy/default Python installations where the path is derived.
    /// This calculates InstallPath based on GlobalConfig and version.
    /// </summary>
    /// <param name="version">The Python version.</param>
    public PyInstallation(PyVersion version)
        : this(version, CalculateDefaultInstallPath(version)) // Delegate to the primary constructor
    { }

    /// <summary>
    /// Constructor for legacy/default Python installations with explicit major, minor, micro.
    /// </summary>
    public PyInstallation(int major, int minor, int micro = 0)
        : this(new PyVersion(major, minor, micro)) { }

    /// <summary>
    /// Calculates the default installation path based on the version.
    /// Used by the legacy constructor.
    /// </summary>
    private static string CalculateDefaultInstallPath(PyVersion version)
    {
        return Path.Combine(GlobalConfig.LibraryDir, "Assets", GetDirectoryNameForVersion(version));
    }

    /// <summary>
    /// Gets the conventional directory name for a given Python version.
    /// This is mainly for deriving legacy paths or for when UV is instructed
    /// to install into a directory with this naming scheme.
    /// </summary>
    /// <param name="version">The Python version.</param>
    /// <param name="precision">How precise the directory name should be (Major.Minor or Major.Minor.Patch).</param>
    /// <returns>The directory name string.</returns>
    public static string GetDirectoryNameForVersion(
        PyVersion version,
        VersionEqualityPrecision precision = VersionEqualityPrecision.MajorMinorPatch
    )
    {
        // Handle the special case for 3.10.11 which was previously just "Python310"
        if (version is { Major: 3, Minor: 10, Micro: 11 } && precision != VersionEqualityPrecision.MajorMinor)
        {
            // If we're checking against the specific 3.10.11 from PyInstallationManager, and precision allows for micro
            if (version == PyInstallationManager.Python_3_10_11)
                return "Python310";
        }

        return precision switch
        {
            VersionEqualityPrecision.MajorMinor => $"Python{version.Major}{version.Minor}",
            _ => $"Python{version.Major}{version.Minor}{version.Micro}",
        };
    }

    public enum VersionEqualityPrecision
    {
        MajorMinor,
        MajorMinorPatch
    }

    /// <summary>
    /// Check if this Python installation appears to be valid by checking for essential files.
    /// (e.g., Python DLL or executable).
    /// </summary>
    public bool Exists()
    {
        if (!Directory.Exists(InstallPath))
            return false;

        // A more robust check might be needed. PythonExePath and PythonDllPath depend on OS.
        // For now, let's check for the DLL on Windows and Exe on others as a primary indicator.
        // Or just check PythonExePath as it should always exist.
        return File.Exists(PythonExePath) || File.Exists(PythonDllPath);
    }

    /// <summary>
    /// Creates a unique identifier for this Python installation
    /// </summary>
    public override string ToString() => $"Python {Version} (at {InstallPath})";

    public override bool Equals(object? obj)
    {
        if (obj is PyInstallation other)
        {
            // Consider installations equal if version and path are the same.
            return Version.Equals(other.Version)
                && StringComparer.OrdinalIgnoreCase.Equals(InstallPath, other.InstallPath);
        }
        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Version, InstallPath.ToLowerInvariant());
    }
}
