namespace StabilityMatrix.Core.Models;

public class CustomVersion : IComparable<CustomVersion>
{
    public int Major { get; set; }
    public int Minor { get; set; }
    public int Patch { get; set; }
    public string? PreRelease { get; set; }

    public CustomVersion() { }

    public CustomVersion(string versionString)
    {
        var parts = versionString.Split(new[] { '-', '.' }, StringSplitOptions.None);
        Major = int.Parse(parts[0]);
        Minor = int.Parse(parts[1]);
        Patch = int.Parse(parts[2]);
        PreRelease = parts.Length > 3 ? string.Join(".", parts.Skip(3)) : null;
    }

    public int CompareTo(CustomVersion? other)
    {
        var result = Major.CompareTo(other?.Major);
        if (result != 0)
            return result;

        result = Minor.CompareTo(other?.Minor);
        if (result != 0)
            return result;

        result = Patch.CompareTo(other?.Patch);
        if (result != 0)
            return result;

        switch (PreRelease)
        {
            case null when other?.PreRelease == null:
                return 0;
            case null:
                return 1;
        }

        if (other?.PreRelease == null)
            return -1;

        return string.Compare(PreRelease, other.PreRelease, StringComparison.Ordinal);
    }

    public static bool operator <(CustomVersion v1, CustomVersion v2)
    {
        return v1.CompareTo(v2) < 0;
    }

    public static bool operator >(CustomVersion v1, CustomVersion v2)
    {
        return v1.CompareTo(v2) > 0;
    }

    public static bool operator <=(CustomVersion v1, CustomVersion v2)
    {
        return v1.CompareTo(v2) <= 0;
    }

    public static bool operator >=(CustomVersion v1, CustomVersion v2)
    {
        return v1.CompareTo(v2) >= 0;
    }

    public override string ToString()
    {
        return $"{Major}.{Minor}.{Patch}" + (PreRelease != null ? $"-{PreRelease}" : string.Empty);
    }
}
