using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace StabilityMatrix.Core.Models.FileInterfaces;

[PublicAPI]
public class FileSystemPath : IEquatable<FileSystemPath>, IEquatable<string>, IFormattable
{
    public string FullPath { get; }

    protected FileSystemPath(string path)
    {
        FullPath = path;
    }

    protected FileSystemPath(FileSystemPath path)
        : this(path.FullPath) { }

    protected FileSystemPath(params string[] paths)
        : this(Path.Combine(paths)) { }

    /// <inheritdoc />
    public override string ToString()
    {
        return FullPath;
    }

    /// <inheritdoc />
    string IFormattable.ToString(string? format, IFormatProvider? formatProvider)
    {
        return ToString(format, formatProvider);
    }

    /// <summary>
    /// Overridable IFormattable.ToString method.
    /// By default, returns <see cref="FullPath"/>.
    /// </summary>
    protected virtual string ToString(string? format, IFormatProvider? formatProvider)
    {
        return FullPath;
    }

    public static bool operator ==(FileSystemPath? left, FileSystemPath? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(FileSystemPath? left, FileSystemPath? right)
    {
        return !Equals(left, right);
    }

    /// <inheritdoc />
    public bool Equals(FileSystemPath? other)
    {
        if (ReferenceEquals(null, other))
            return false;
        if (ReferenceEquals(this, other))
            return true;

        return string.Equals(
            GetNormalizedPath(FullPath),
            GetNormalizedPath(other.FullPath),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal
        );
    }

    /// <inheritdoc />
    public bool Equals(string? other)
    {
        if (ReferenceEquals(null, other))
            return false;

        return Equals((FileSystemPath)other);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj))
            return false;
        if (ReferenceEquals(this, obj))
            return true;
        if (GetType() != obj.GetType())
            return false;
        return Equals((FileSystemPath)obj);
    }

    /// <summary>
    /// Normalize a path to a consistent format for comparison.
    /// </summary>
    /// <param name="path">Path to normalize.</param>
    /// <returns>Normalized path.</returns>
    [return: NotNullIfNotNull(nameof(path))]
    private static string? GetNormalizedPath(string? path)
    {
        // Return null or empty paths as-is
        if (string.IsNullOrEmpty(path))
        {
            return path;
        }

        if (Uri.TryCreate(path, UriKind.RelativeOrAbsolute, out var uri))
        {
            if (uri.IsAbsoluteUri)
            {
                path = uri.LocalPath;
            }
        }

        // Get full path if possible, ignore errors like invalid chars or too long
        try
        {
            path = Path.GetFullPath(path);
        }
        catch (SystemException) { }

        return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(GetType().GetHashCode(), FullPath.GetHashCode());
    }

    // Implicit conversions to and from string
    public static implicit operator string(FileSystemPath path) => path.FullPath;

    public static implicit operator FileSystemPath(string path) => new(path);
}
