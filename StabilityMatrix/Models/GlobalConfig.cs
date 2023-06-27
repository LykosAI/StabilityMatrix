using System;

namespace StabilityMatrix.Models;

public static class GlobalConfig
{
    private static string? libraryDir;
    
    /// <summary>
    /// Absolute path to the library directory.
    /// Needs to be set by SettingsManager.TryFindLibrary() before being accessed.
    /// </summary>
    /// <exception cref="Exception"></exception>
    public static string LibraryDir
    {
        get
        {
            if (string.IsNullOrEmpty(libraryDir))
            {
                throw new Exception("GlobalConfig.LibraryDir was not set before being accessed.");
            }
            return libraryDir;
        }
        set => libraryDir = value;
    }
}
