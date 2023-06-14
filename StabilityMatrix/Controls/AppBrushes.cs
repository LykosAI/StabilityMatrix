using System.Windows.Media;

namespace StabilityMatrix.Controls;

public static class AppBrushes
{
    public static readonly SolidColorBrush SuccessGreen = FromHex("#4caf50")!;
    public static readonly SolidColorBrush FailedRed = FromHex("#f44336")!;
    public static readonly SolidColorBrush WarningYellow = FromHex("#ffeb3b")!;

    private static SolidColorBrush? FromHex(string hex)
    {
        return new BrushConverter().ConvertFrom(hex) as SolidColorBrush;
    }
}
