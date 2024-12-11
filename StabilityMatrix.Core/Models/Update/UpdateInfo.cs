using System.Globalization;
using System.Text.Json.Serialization;
using Semver;
using StabilityMatrix.Core.Converters.Json;
using StabilityMatrix.Core.Extensions;

namespace StabilityMatrix.Core.Models.Update;

public record UpdateInfo
{
    [JsonConverter(typeof(SemVersionJsonConverter))]
    public required SemVersion Version { get; init; }

    public required DateTimeOffset ReleaseDate { get; init; }

    public UpdateChannel Channel { get; init; }

    public UpdateType Type { get; init; }

    public required Uri Url { get; init; }

    public required Uri Changelog { get; init; }

    /// <summary>
    /// Blake3 hash of the file
    /// </summary>
    public required string HashBlake3 { get; init; }

    /// <summary>
    /// ED25519 signature of the semicolon separated string:
    /// "version + releaseDate + channel + type + url + changelog + hash_blake3"
    /// verifiable using our stored public key
    /// </summary>
    public required string Signature { get; init; }

    /// <summary>
    /// Data for use in signature verification.
    /// Semicolon separated string of fields:
    /// "version, releaseDate, channel, type, url, changelog, hashBlake3"
    /// </summary>
    public string GetSignedData()
    {
        var channel = Channel.GetStringValue().ToLowerInvariant();
        var date = FormatDateTimeOffsetInvariant(ReleaseDate);
        return $"{Version};{date};{channel};" + $"{(int)Type};{Url};{Changelog};" + $"{HashBlake3}";
    }

    /// <summary>
    /// Format a DatetimeOffset to a culture invariant string for use in signature verification.
    /// </summary>
    private static string FormatDateTimeOffsetInvariant(DateTimeOffset dateTimeOffset)
    {
        return dateTimeOffset.ToString(
            @"yyyy-MM-ddTHH\:mm\:ss.ffffffzzz",
            CultureInfo.InvariantCulture
        );
    }
}
