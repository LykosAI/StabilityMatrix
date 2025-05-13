using System;
using System.Text.RegularExpressions;

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

    // Inside PyVersion.cs (or a new PyVersionParser.cs utility class)

    public static bool TryParseFromComplexString(string versionString, out PyVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(versionString))
            return false;

        // Regex to capture major.minor.micro and optional pre-release (e.g., a6, rc1)
        // It tries to be greedy on the numeric part.
        var match = Regex.Match(
            versionString,
            @"^(?<major>\d+)(?:\.(?<minor>\d+))?(?:\.(?<micro>\d+))?(?:[a-zA-Z]+\d*)?$"
        );

        if (!match.Success)
            return false;

        if (!int.TryParse(match.Groups["major"].Value, out var major))
            return false;

        var minor = 0;
        if (match.Groups["minor"].Success && !string.IsNullOrEmpty(match.Groups["minor"].Value))
        {
            if (!int.TryParse(match.Groups["minor"].Value, out minor))
                return false;
        }

        var micro = 0;
        if (match.Groups["micro"].Success && !string.IsNullOrEmpty(match.Groups["micro"].Value))
        {
            if (!int.TryParse(match.Groups["micro"].Value, out micro))
                return false;
        }

        version = new PyVersion(major, minor, micro);
        return true;
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
