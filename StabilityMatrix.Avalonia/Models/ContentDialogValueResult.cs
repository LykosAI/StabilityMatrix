using System.Diagnostics.CodeAnalysis;
using FluentAvalonia.UI.Controls;

namespace StabilityMatrix.Avalonia.Models;

public record ContentDialogValueResult<T>(ContentDialogResult Result, [property: AllowNull] T Value)
{
    public bool IsNone => Result == ContentDialogResult.None;

    public bool IsPrimary => Result == ContentDialogResult.Primary;

    public bool IsSecondary => Result == ContentDialogResult.Secondary;
}
