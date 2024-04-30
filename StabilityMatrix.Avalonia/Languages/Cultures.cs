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

    public static readonly Dictionary<string, CultureInfo> SupportedCulturesByCode =
        new()
        {
            ["en-US"] = Default,
            ["ja-JP"] = new CultureInfo("ja-JP"),
            ["zh-Hans"] = new CultureInfo("zh-Hans"),
            ["zh-Hant"] = new CultureInfo("zh-Hant"),
            ["it-IT"] = new CultureInfo("it-IT"),
            ["fr-FR"] = new CultureInfo("fr-FR"),
            ["es"] = new CultureInfo("es"),
            ["ru-RU"] = new CultureInfo("ru-RU"),
            ["tr-TR"] = new CultureInfo("tr-TR"),
            ["de"] = new CultureInfo("de"),
            ["pt-PT"] = new CultureInfo("pt-PT"),
            ["pt-BR"] = new CultureInfo("pt-BR")
        };

    public static IReadOnlyList<CultureInfo> SupportedCultures =>
        SupportedCulturesByCode.Values.ToImmutableList();

    public static CultureInfo GetSupportedCultureOrDefault(string? cultureCode)
    {
        if (cultureCode is null || !SupportedCulturesByCode.TryGetValue(cultureCode, out var culture))
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
        if (cultureCode is null || !SupportedCulturesByCode.TryGetValue(cultureCode, out var culture))
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
