using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using StabilityMatrix.Native.Abstractions;
#if Windows
using StabilityMatrix.Native.Windows;
#endif

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
        RecycleBin = new NativeRecycleBinProvider();
#endif
    }
}
