using System.Diagnostics.CodeAnalysis;

namespace StabilityMatrix.Native.Abstractions;

[Flags]
public enum NativeFileOperationFlags : uint
{
    /// <summary>
    /// Do not display a progress dialog.
    /// </summary>
    Silent = 1 << 0,

    /// <summary>
    /// Display a warning if files are being permanently deleted.
    /// </summary>
    WarnOnPermanentDelete = 1 << 1,

    /// <summary>
    /// Do not ask the user to confirm the operation.
    /// </summary>
    NoConfirmation = 1 << 2,
}

public static class NativeFileOperationFlagsExtensions
{
    [SuppressMessage("ReSharper", "CommentTypo")]
    public static void ToWindowsFileOperationFlags(
        this NativeFileOperationFlags flags,
        ref uint windowsFileOperationFlags
    )
    {
        if (flags.HasFlag(NativeFileOperationFlags.Silent))
        {
            windowsFileOperationFlags |= 0x0004; // FOF_SILENT
        }

        if (flags.HasFlag(NativeFileOperationFlags.WarnOnPermanentDelete))
        {
            windowsFileOperationFlags |= 0x4000; // FOF_WANTNUKEWARNING
        }

        if (flags.HasFlag(NativeFileOperationFlags.NoConfirmation))
        {
            windowsFileOperationFlags |= 0x0010; // FOF_NOCONFIRMATION
        }
    }
}
