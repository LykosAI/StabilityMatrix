using System;
using System.Diagnostics.CodeAnalysis;
using Projektanker.Icons.Avalonia;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Models.TagCompletion;
using StabilityMatrix.Avalonia.Styles;

namespace StabilityMatrix.Avalonia.Controls.CodeCompletion;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public static class CompletionIcons
{
    public static readonly IconData General =
        new() { FAIcon = "fa-solid fa-star-of-life", Foreground = ThemeColors.LightSteelBlue, };

    public static readonly IconData Artist =
        new() { FAIcon = "fa-solid fa-palette", Foreground = ThemeColors.AmericanYellow, };

    public static readonly IconData Character =
        new() { FAIcon = "fa-solid fa-user", Foreground = ThemeColors.LuminousGreen, };

    public static readonly IconData Copyright =
        new() { FAIcon = "fa-solid fa-copyright", Foreground = ThemeColors.DeepMagenta, };

    public static readonly IconData Species =
        new()
        {
            FAIcon = "fa-solid fa-dragon",
            FontSize = 14,
            Foreground = ThemeColors.HalloweenOrange,
        };

    public static readonly IconData Invalid =
        new()
        {
            FAIcon = "fa-solid fa-question",
            Foreground = ThemeColors.CompletionForegroundBrush,
        };

    public static readonly IconData Keyword =
        new() { FAIcon = "fa-solid fa-key", Foreground = ThemeColors.CompletionForegroundBrush, };

    public static readonly IconData Model =
        new() { FAIcon = "fa-solid fa-cube", Foreground = ThemeColors.CompletionForegroundBrush, };

    public static readonly IconData ModelType =
        new() { FAIcon = "fa-solid fa-shapes", Foreground = ThemeColors.BrilliantAzure, };

    public static IconData? GetIconForTagType(TagType tagType)
    {
        return tagType switch
        {
            TagType.General => General,
            TagType.Artist => Artist,
            TagType.Character => Character,
            TagType.Species => Species,
            TagType.Invalid => Invalid,
            TagType.Copyright => Copyright,
            _ => null
        };
    }
}
