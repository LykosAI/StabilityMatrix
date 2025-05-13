namespace StabilityMatrix.Core.Python;

/// <summary>
/// Represents information about a Python installation as discovered or managed by UV.
/// </summary>
public readonly record struct UvPythonInfo(
    PyVersion Version,
    string InstallPath, // Full path to the root of the Python installation
    bool IsInstalled, // True if UV reports it as installed
    string? Source, // e.g., "cpython", "pypy" - from 'uv python list'
    string? Architecture, // e.g., "x86_64" - from 'uv python list'
    string? Os, // e.g., "unknown-linux-gnu" - from 'uv python list'
    string? Key // The unique key/name uv uses, e.g., "cpython@3.10.13" or "3.10.13"
);
