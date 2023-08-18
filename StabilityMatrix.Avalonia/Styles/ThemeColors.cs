using Avalonia;
using Avalonia.Media;

namespace StabilityMatrix.Avalonia.Styles;

public static class ThemeColors
{
    public static readonly SolidColorBrush ThemeGreen = SolidColorBrush.Parse("#4caf50");
    public static readonly SolidColorBrush ThemeRed = SolidColorBrush.Parse("#f44336");
    public static readonly SolidColorBrush ThemeYellow = SolidColorBrush.Parse("#ffeb3b");
    
    public static readonly SolidColorBrush AmericanYellow = SolidColorBrush.Parse("#f2ac08");
    public static readonly SolidColorBrush HalloweenOrange = SolidColorBrush.Parse("#ed5D1f");
    public static readonly SolidColorBrush LightSteelBlue = SolidColorBrush.Parse("#b4c7d9");
    public static readonly SolidColorBrush DeepMagenta = SolidColorBrush.Parse("#dd00dd");
    public static readonly SolidColorBrush LuminousGreen = SolidColorBrush.Parse("#00aa00");

    public static readonly SolidColorBrush CompletionSelectionBackgroundBrush = 
        SolidColorBrush.Parse("#2E436E");
    public static readonly SolidColorBrush CompletionSelectionForegroundBrush = 
        SolidColorBrush.Parse("#5389F4");
    public static readonly SolidColorBrush CompletionForegroundBrush =
        SolidColorBrush.Parse("#B4B8BF");
    
    public static readonly SolidColorBrush EditorSelectionBrush = 
        SolidColorBrush.Parse("#214283");
}
