using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace StabilityMatrix.Core.Models;

/// <summary>
/// Union of either Tag or Branch + CommitSha.
/// </summary>
public record GitVersion : IFormattable, IUtf8SpanParsable<GitVersion>
{
    public string? Tag { get; init; }

    public string? Branch { get; init; }

    public string? CommitSha { get; init; }

    /// <inheritdoc />
    public override string ToString()
    {
        return ToString(null, null);
    }

    /// <inheritdoc />
    /// <remarks>
    /// - The "O" format specifier can be used to format for round-trip serialization with full commit SHAs.
    /// - The "G" format specifier uses abbreviated commit SHAs (first 7 characters).
    /// "O" is used by default.
    /// </remarks>
    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        switch (format)
        {
            case "G":
            {
                if (!string.IsNullOrEmpty(Tag))
                {
                    return Tag;
                }

                if (!string.IsNullOrEmpty(Branch) && !string.IsNullOrEmpty(CommitSha))
                {
                    return $"{Branch}@{CommitSha[..7]}";
                }

                if (!string.IsNullOrEmpty(Branch))
                {
                    return Branch;
                }

                return !string.IsNullOrEmpty(CommitSha) ? CommitSha[..7] : "";
            }
            case "O":
            case null:
            {
                if (!string.IsNullOrEmpty(Tag))
                {
                    return Tag;
                }

                if (!string.IsNullOrEmpty(Branch) && !string.IsNullOrEmpty(CommitSha))
                {
                    return $"{Branch}@{CommitSha}";
                }

                if (!string.IsNullOrEmpty(Branch))
                {
                    return Branch;
                }

                return !string.IsNullOrEmpty(CommitSha) ? CommitSha : "";
            }
            default:
                throw new FormatException($"The {format} format specifier is not supported.");
        }
    }

    public static bool TryParse(
        ReadOnlySpan<byte> utf8Text,
        IFormatProvider? provider,
        [MaybeNullWhen(false)] out GitVersion result
    )
    {
        return TryParse(utf8Text, provider, out result, false);
    }

    private static bool TryParse(
        ReadOnlySpan<byte> utf8Source,
        IFormatProvider? provider,
        [MaybeNullWhen(false)] out GitVersion result,
        bool throwOnFailure
    )
    {
        result = null;

        try
        {
            var source = Encoding.UTF8.GetString(utf8Source);
            if (string.IsNullOrEmpty(source))
            {
                return false;
            }

            if (source.Contains('@'))
            {
                var parts = source.Split('@');
                if (parts.Length == 2)
                {
                    var branch = parts[0];
                    var commitSha = parts[1];

                    result = new GitVersion { Branch = branch, CommitSha = commitSha };
                    return true;
                }
            }
            else
            {
                result = new GitVersion { Tag = source };
                return true;
            }
        }
        catch
        {
            if (throwOnFailure)
            {
                throw;
            }
            return false;
        }

        return false;
    }

    public static GitVersion Parse(ReadOnlySpan<byte> utf8Source, IFormatProvider? provider)
    {
        if (TryParse(utf8Source, provider, out var result))
        {
            return result;
        }

        throw new FormatException("Invalid GitVersion format.");
    }
}
