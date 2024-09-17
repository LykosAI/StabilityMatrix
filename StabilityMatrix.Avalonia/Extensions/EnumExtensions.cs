using System;
using StabilityMatrix.Core.Extensions;

namespace StabilityMatrix.Avalonia.Extensions;

public static class EnumExtensions
{
    public static bool TryParseEnumStringValue<T>(string? value, T defaultValue, out T result)
        where T : Enum
    {
        result = defaultValue;

        if (value == null)
            return false;

        foreach (T enumValue in Enum.GetValues(typeof(T)))
        {
            if (!enumValue.GetStringValue().Equals(value, StringComparison.OrdinalIgnoreCase))
                continue;

            result = enumValue;
            return true;
        }

        return false;
    }
}
