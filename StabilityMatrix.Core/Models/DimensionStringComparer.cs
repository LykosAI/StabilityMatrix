using System.Collections;
using System.Text.RegularExpressions;

namespace StabilityMatrix.Core.Models;

public partial class DimensionStringComparer : IComparer, IComparer<string>
{
    public static readonly DimensionStringComparer Instance = new();

    /// <summary>
    /// Compares two dimension strings (like "1024 x 768") by the first numeric value.
    /// </summary>
    /// <param name="x">First dimension string to compare</param>
    /// <param name="y">Second dimension string to compare</param>
    /// <returns>
    /// A negative value if x comes before y;
    /// zero if x equals y;
    /// a positive value if x comes after y
    /// </returns>
    public int Compare(object? x, object? y)
    {
        // Handle null cases
        if (x == null && y == null)
            return 0;
        if (x == null)
            return -1;
        if (y == null)
            return 1;

        if (x is not string xStr || y is not string yStr)
            throw new ArgumentException("Both arguments must be strings.");

        // Extract the first number from each string
        var firstX = ExtractFirstNumber(xStr);
        var firstY = ExtractFirstNumber(yStr);

        // Compare the first numbers
        return firstX.CompareTo(firstY);
    }

    public int Compare(string? x, string? y)
    {
        // Handle null cases
        if (x == null && y == null)
            return 0;
        if (x == null)
            return -1;
        if (y == null)
            return 1;

        // Extract the first number from each string
        var firstX = ExtractFirstNumber(x);
        var firstY = ExtractFirstNumber(y);

        // Compare the first numbers
        return firstX.CompareTo(firstY);
    }

    /// <summary>
    /// Extracts the first numeric value from a dimension string.
    /// </summary>
    /// <param name="dimensionString">String in format like "1024 x 768"</param>
    /// <returns>The first numeric value or 0 if no number is found</returns>
    private static int ExtractFirstNumber(string dimensionString)
    {
        // Use regex to find the first number in the string
        var match = NumberRegex().Match(dimensionString);

        if (match.Success && int.TryParse(match.Value, out var result))
        {
            return result;
        }

        return 0; // Return 0 if no number found
    }

    [GeneratedRegex(@"\d+")]
    private static partial Regex NumberRegex();
}
