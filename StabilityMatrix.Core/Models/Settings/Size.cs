namespace StabilityMatrix.Core.Models.Settings;

public record struct Size(double Width, double Height)
{
    public static Size operator +(Size current, Size other) =>
        new(current.Width + other.Width, current.Height + other.Height);

    public static Size operator -(Size current, Size other) =>
        new(current.Width - other.Width, current.Height - other.Height);
}
