using System;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Data.Converters;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Avalonia.Converters;

[SuppressMessage("ReSharper", "LocalizableElement")]
public static class FileSizeConverters
{
    public static FuncValueConverter<object?, string> HumanizeFileSizeConverter { get; } =
        new(value =>
        {
            var size = Convert.ToDouble(value);

            string[] sizeUnits = ["KB", "MB", "GB", "TB"];

            var unitIndex = 0;

            while (size >= 1000 && unitIndex < sizeUnits.Length - 1)
            {
                size /= 1000;
                unitIndex++;
            }

            return $"{size:0.##} {sizeUnits[unitIndex]}";
        });

    public static FuncValueConverter<object?, string> HumanizeBinaryFileSizeConverter { get; } =
        new(value =>
        {
            var size = Convert.ToDouble(value);

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
