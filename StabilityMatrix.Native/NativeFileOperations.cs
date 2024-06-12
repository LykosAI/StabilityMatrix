using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using StabilityMatrix.Native.Abstractions;

namespace StabilityMatrix.Native;

[PublicAPI]
public static class NativeFileOperations
{
    public static INativeRecycleBinProvider? RecycleBin { get; }

    [MemberNotNullWhen(true, nameof(RecycleBin))]
    public static bool IsRecycleBinAvailable => RecycleBin is not null;

    static NativeFileOperations()
    {
#if Windows
        if (!OperatingSystem.IsWindows())
        {
            Debug.Fail(
                $"Assembly of {nameof(NativeFileOperations)} was compiled for Windows, "
                    + $"the current OS is '{Environment.OSVersion}'"
            );
            return;
        }

        RecycleBin = new Windows.NativeRecycleBinProvider();
#elif OSX
        if (!OperatingSystem.IsMacOS())
        {
            Debug.Fail(
                $"Assembly of {nameof(NativeFileOperations)} was compiled for macOS, "
                    + $"the current OS is '{Environment.OSVersion}'"
            );
            return;
        }

        RecycleBin = new macOS.NativeRecycleBinProvider();
#endif
    }
}
