using System.Text.Json;
using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models;

/// <summary>
/// User-owned metadata for a local model, stored next to the model file as
/// <c>{name}.cm-user.json</c>. Kept separate from <see cref="ConnectedModelInfo"/> so that a
/// remote metadata refresh can never overwrite what the user wrote, and so that a model with
/// no remote metadata can still carry user data without being treated as "connected".
/// </summary>
public sealed class UserModelMetadata : IEquatable<UserModelMetadata>
{
    [JsonIgnore]
    public const string FileExtension = ".cm-user.json";

    /// <summary>
    /// User-defined category names this model belongs to. Names are matched case-insensitively
    /// against <see cref="Settings.ModelCategory"/> entries in settings.
    /// </summary>
    public List<string> Categories { get; set; } = [];

    /// <summary>
    /// True when nothing is set, in which case the sidecar file should not exist.
    /// </summary>
    [JsonIgnore]
    public bool IsEmpty => Categories.Count == 0;

    public static UserModelMetadata? FromJson(string json)
    {
        return JsonSerializer.Deserialize(json, UserModelMetadataSerializerContext.Default.UserModelMetadata);
    }

    public string ToJson()
    {
        return JsonSerializer.Serialize(this, UserModelMetadataSerializerContext.Default.UserModelMetadata);
    }

    /// <summary>
    /// Path of the sidecar file for the model at <paramref name="modelFilePath"/>.
    /// </summary>
    public static string GetSidecarPath(string modelFilePath)
    {
        var directory = Path.GetDirectoryName(modelFilePath) ?? string.Empty;
        return Path.Combine(directory, Path.GetFileNameWithoutExtension(modelFilePath) + FileExtension);
    }

    public bool Equals(UserModelMetadata? other)
    {
        if (other is null)
            return false;
        if (ReferenceEquals(this, other))
            return true;
        return Categories.SequenceEqual(other.Categories, StringComparer.Ordinal);
    }

    public override bool Equals(object? obj) => obj is UserModelMetadata other && Equals(other);

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        foreach (var category in Categories)
        {
            hashCode.Add(category, StringComparer.Ordinal);
        }
        return hashCode.ToHashCode();
    }
}

[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Default,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
)]
[JsonSerializable(typeof(UserModelMetadata))]
internal partial class UserModelMetadataSerializerContext : JsonSerializerContext;
