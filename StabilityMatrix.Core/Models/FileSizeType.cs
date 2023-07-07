using System.Globalization;

namespace StabilityMatrix.Core.Models;

public class FileSizeType
{
    public double SizeInKB { get; private set; }

    public string HumanReadableRepresentation { get; private set; }

    public FileSizeType(double sizeInKB)
    {
        SizeInKB = sizeInKB;
        HumanReadableRepresentation = ConvertToHumanReadable();
    }

    private string ConvertToHumanReadable()
    {
        var sizeUnits = new string[] { "KB", "MB", "GB", "TB" };
        var size = SizeInKB;
        var unitIndex = 0;

        while (size >= 1024 && unitIndex < sizeUnits.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return string.Format("{0} {1}", size.ToString("0.##", CultureInfo.InvariantCulture), sizeUnits[unitIndex]);
    }

    public override string ToString()
    {
        return HumanReadableRepresentation;
    }
}
