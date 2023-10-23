using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using StabilityMatrix.Core.Helper;

namespace StabilityMatrix.Avalonia.Languages;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public static class Cultures
{
    public static CultureInfo Default { get; } = new("en-US");

    public static CultureInfo? Current => Resources.Culture;

    public static readonly Dictionary<string, CultureInfo> SupportedCulturesByCode = new Dictionary<
        string,
        CultureInfo
    >
    {
        ["en-US"] = Default,
        ["ja-JP"] = new("ja-JP"),
        ["zh-Hans"] = new("zh-Hans"),
        ["zh-Hant"] = new("zh-Hant"),
        ["it-IT"] = new("it-IT"),
        ["fr-FR"] = new("fr-FR"),
        ["es"] = new("es"),
        ["ru-RU"] = new("ru-RU")
    };

    public static IReadOnlyList<CultureInfo> SupportedCultures =>
        SupportedCulturesByCode.Values.ToImmutableList();

    public static CultureInfo GetSupportedCultureOrDefault(string? cultureCode)
    {
        if (
            cultureCode is null
            || !SupportedCulturesByCode.TryGetValue(cultureCode, out var culture)
        )
        {
            return Default;
        }

        return culture;
    }

    public static void SetSupportedCultureOrDefault(string? cultureCode)
    {
        if (!TrySetSupportedCulture(cultureCode))
        {
            TrySetSupportedCulture(Default);
        }
    }

    public static bool TrySetSupportedCulture(string? cultureCode)
    {
        if (
            cultureCode is null
            || !SupportedCulturesByCode.TryGetValue(cultureCode, out var culture)
        )
        {
            return false;
        }

        if (Current?.Name != culture.Name)
        {
            Resources.Culture = culture;
            EventManager.Instance.OnCultureChanged(culture);
        }

        return true;
    }

    public static bool TrySetSupportedCulture(CultureInfo? cultureInfo)
    {
        return cultureInfo is not null && TrySetSupportedCulture(cultureInfo.Name);
    }
}
