using System.Diagnostics.CodeAnalysis;
using Avalonia.Data.Converters;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Avalonia.Converters;

[SuppressMessage("ReSharper", "LocalizableElement")]
public static class FileSizeConverters
{
    public static FuncValueConverter<double?, string> HumanizeFileSizeConverter { get; } =
        new(value =>
        {
            if (value is not { } size)
            {
                return string.Empty;
            }

            string[] sizeUnits = ["KB", "MB", "GB", "TB"];

            var unitIndex = 0;

            while (size >= 1000 && unitIndex < sizeUnits.Length - 1)
            {
                size /= 1000;
                unitIndex++;
            }

            return $"{size:0.##} {sizeUnits[unitIndex]}";
        });

    public static FuncValueConverter<double?, string> HumanizeBinaryFileSizeConverter { get; } =
        new(value =>
        {
            if (value is not { } size)
            {
                return string.Empty;
            }

            string[] sizeUnits = ["KiB", "MiB", "GiB", "TiB"];

            var unitIndex = 0;

            while (size >= 1024 && unitIndex < sizeUnits.Length - 1)
            {
                size /= 1024;
                unitIndex++;
            }

            return $"{size:0.##} {sizeUnits[unitIndex]}";
        });
}
