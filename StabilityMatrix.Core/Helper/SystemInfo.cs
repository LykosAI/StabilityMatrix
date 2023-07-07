using System.Runtime.InteropServices;

namespace StabilityMatrix.Core.Helper;

public static class SystemInfo
{
    [DllImport("UXTheme.dll", SetLastError = true, EntryPoint = "#138")] 
    public static extern bool ShouldUseDarkMode();
}
