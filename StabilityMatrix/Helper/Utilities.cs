using System.Reflection;

namespace StabilityMatrix.Helper;

public static class Utilities
{
    public static string GetAppVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        return version == null
            ? "(Unknown)"
            : $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
    }
}
