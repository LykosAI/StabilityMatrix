using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models;

/// <summary>
/// The user-owned part of a model's <c>.cm-info.json</c> (<see cref="ConnectedModelInfo.User"/>).
/// Remote metadata writers never populate this block, and the save path carries it across from
/// disk when the object being written has none, so a metadata refresh cannot drop it.
/// </summary>
public sealed class UserModelMetadata : IEquatable<UserModelMetadata>
{
    /// <summary>
    /// User-defined category names this model belongs to. Matched case-insensitively against
    /// <see cref="Settings.ModelCategory"/> entries in settings.
    /// </summary>
    public List<string> Categories { get; set; } = [];

    /// <summary>
    /// Set when the user disconnected the model from its source: bulk metadata scans leave it
    /// alone. An explicit per-model "Find Connected Metadata" still runs and clears this.
    /// </summary>
    public bool ExcludeFromMetadataSearch { get; set; }

    /// <summary>
    /// True when nothing is set, in which case the block carries no information worth keeping.
    /// </summary>
    [JsonIgnore]
    public bool IsEmpty => Categories.Count == 0 && !ExcludeFromMetadataSearch;

    public UserModelMetadata Clone()
    {
        return new UserModelMetadata
        {
            Categories = [.. Categories],
            ExcludeFromMetadataSearch = ExcludeFromMetadataSearch,
        };
    }

    public bool Equals(UserModelMetadata? other)
    {
        if (other is null)
            return false;
        if (ReferenceEquals(this, other))
            return true;
        return ExcludeFromMetadataSearch == other.ExcludeFromMetadataSearch
            && Categories.SequenceEqual(other.Categories, StringComparer.Ordinal);
    }

    public override bool Equals(object? obj) => obj is UserModelMetadata other && Equals(other);

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(ExcludeFromMetadataSearch);
        foreach (var category in Categories)
        {
            hashCode.Add(category, StringComparer.Ordinal);
        }
        return hashCode.ToHashCode();
    }
}
