using System.Diagnostics.CodeAnalysis;

namespace StabilityMatrix.Core.Helper;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public static class Size
{
    public const ulong KiB = 1024;
    public const ulong MiB = KiB * 1024;
    public const ulong GiB = MiB * 1024;

    private static string TrimZero(string value)
    {
        return value.TrimEnd('0').TrimEnd('.');
    }

    public static string FormatBytes(ulong bytes, bool trimZero = false)
    {
        return bytes switch
        {
            < KiB => $"{bytes:0} Bytes",
            < MiB
                => (
                    trimZero
                        ? $"{bytes / (double)KiB:0.0}".TrimEnd('0').TrimEnd('.')
                        : $"{bytes / (double)KiB:0.0}"
                ) + " KiB",
            < GiB
                => (
                    trimZero
                        ? $"{bytes / (double)MiB:0.0}".TrimEnd('0').TrimEnd('.')
                        : $"{bytes / (double)MiB:0.0}"
                ) + " MiB",
            _
                => (
                    trimZero
                        ? $"{bytes / (double)GiB:0.0}".TrimEnd('0').TrimEnd('.')
                        : $"{bytes / (double)GiB:0.0}"
                ) + " GiB"
        };
    }

    public static string FormatBase10Bytes(ulong bytes, bool trimZero = false)
    {
        return bytes switch
        {
            < KiB => $"{bytes:0} Bytes",
            < MiB
                => (
                    trimZero
                        ? $"{bytes / (double)KiB:0.0}".TrimEnd('0').TrimEnd('.')
                        : $"{bytes / (double)KiB:0.0}"
                ) + " KB",
            < GiB
                => (
                    trimZero
                        ? $"{bytes / (double)MiB:0.0}".TrimEnd('0').TrimEnd('.')
                        : $"{bytes / (double)MiB:0.0}"
                ) + " MB",
            _
                => (
                    trimZero
                        ? $"{bytes / (double)GiB:0.00}".TrimEnd('0').TrimEnd('.')
                        : $"{bytes / (double)GiB:0.00}"
                ) + " GB"
        };
    }

    public static string FormatBase10Bytes(long bytes)
    {
        return FormatBase10Bytes(Convert.ToUInt64(bytes));
    }
}
