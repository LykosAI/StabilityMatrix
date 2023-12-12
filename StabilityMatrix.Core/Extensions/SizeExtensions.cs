using System.Drawing;

namespace StabilityMatrix.Core.Extensions;

public static class SizeExtensions
{
    public static Size WithScale(this Size size, double scale)
    {
        return new Size((int)Math.Floor(size.Width * scale), (int)Math.Floor(size.Height * scale));
    }
}
