using System.Runtime.InteropServices;
using NLog;

namespace StabilityMatrix.Core.Helper;

public static class SystemInfo
{
    public const long Gigabyte = 1024 * 1024 * 1024;
    public const long Megabyte = 1024 * 1024;

    [DllImport("UXTheme.dll", SetLastError = true, EntryPoint = "#138")]
    public static extern bool ShouldUseDarkMode();

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetDiskFreeSpaceEx(
        string lpDirectoryName,
        out long lpFreeBytesAvailable,
        out long lpTotalNumberOfBytes,
        out long lpTotalNumberOfFreeBytes
    );

    public static long? GetDiskFreeSpaceBytes(string path)
    {
        long? freeBytes = null;
        try
        {
            if (Compat.IsWindows)
            {
                if (GetDiskFreeSpaceEx(path, out var freeBytesOut, out var _, out var _))
                    freeBytes = freeBytesOut;
            }

            if (freeBytes == null)
            {
                var drive = new DriveInfo(path);
                freeBytes = drive.AvailableFreeSpace;
            }
        }
        catch (Exception e)
        {
            LogManager.GetCurrentClassLogger().Error(e);
        }

        return freeBytes;
    }
}
