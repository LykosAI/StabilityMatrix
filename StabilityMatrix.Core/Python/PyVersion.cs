using System;

namespace StabilityMatrix.Core.Python;

/// <summary>
/// Represents a Python version
/// </summary>
public readonly struct PyVersion : IEquatable<PyVersion>, IComparable<PyVersion>
{
    /// <summary>
    /// Major version number
    /// </summary>
    public int Major { get; }

    /// <summary>
    /// Minor version number
    /// </summary>
    public int Minor { get; }

    /// <summary>
    /// Micro/patch version number
    /// </summary>
    public int Micro { get; }

    /// <summary>
    /// Creates a new PyVersion
    /// </summary>
    public PyVersion(int major, int minor, int micro)
    {
        Major = major;
        Minor = minor;
        Micro = micro;
    }

    /// <summary>
    /// Parses a version string in the format "major.minor.micro"
    /// </summary>
    public static PyVersion Parse(string versionString)
    {
        var parts = versionString.Split('.');
        if (parts.Length is < 2 or > 3)
        {
            throw new ArgumentException($"Invalid version format: {versionString}", nameof(versionString));
        }

        if (!int.TryParse(parts[0], out var major) || !int.TryParse(parts[1], out var minor))
        {
            throw new ArgumentException($"Invalid version format: {versionString}", nameof(versionString));
        }

        var micro = 0;
        if (parts.Length <= 2)
            return new PyVersion(major, minor, micro);

        if (!int.TryParse(parts[2], out micro))
        {
            throw new ArgumentException($"Invalid version format: {versionString}", nameof(versionString));
        }

        return new PyVersion(major, minor, micro);
    }

    /// <summary>
    /// Tries to parse a version string
    /// </summary>
    public static bool TryParse(string versionString, out PyVersion version)
    {
        try
        {
            version = Parse(versionString);
            return true;
        }
        catch
        {
            version = default;
            return false;
        }
    }

    /// <summary>
    /// Returns the version as a string in the format "major.minor.micro"
    /// </summary>
    public override string ToString() => $"{Major}.{Minor}.{Micro}";

    /// <summary>
    /// Checks if this version equals another version
    /// </summary>
    public bool Equals(PyVersion other) =>
        Major == other.Major && Minor == other.Minor && Micro == other.Micro;

    /// <summary>
    /// Compares this version to another version
    /// </summary>
    public int CompareTo(PyVersion other)
    {
        var majorComparison = Major.CompareTo(other.Major);
        if (majorComparison != 0)
            return majorComparison;

        var minorComparison = Minor.CompareTo(other.Minor);
        if (minorComparison != 0)
            return minorComparison;

        return Micro.CompareTo(other.Micro);
    }

    /// <summary>
    /// Checks if this version equals another object
    /// </summary>
    public override bool Equals(object? obj) => obj is PyVersion other && Equals(other);

    /// <summary>
    /// Gets a hash code for this version
    /// </summary>
    public override int GetHashCode() => HashCode.Combine(Major, Minor, Micro);

    public static bool operator ==(PyVersion left, PyVersion right) => left.Equals(right);

    public static bool operator !=(PyVersion left, PyVersion right) => !left.Equals(right);

    public static bool operator <(PyVersion left, PyVersion right) => left.CompareTo(right) < 0;

    public static bool operator <=(PyVersion left, PyVersion right) => left.CompareTo(right) <= 0;

    public static bool operator >(PyVersion left, PyVersion right) => left.CompareTo(right) > 0;

    public static bool operator >=(PyVersion left, PyVersion right) => left.CompareTo(right) >= 0;

    public string StringValue => $"{Major}.{Minor}.{Micro}";
}
