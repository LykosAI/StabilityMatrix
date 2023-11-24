using System.Text.Json.Serialization;
using StabilityMatrix.Core.Converters.Json;

namespace StabilityMatrix.Core.Models.Settings;

/// <summary>
/// Teaching tip names
/// </summary>
[JsonConverter(typeof(StringJsonConverter<TeachingTip>))]
public record TeachingTip(string Value) : StringValue(Value)
{
    public static TeachingTip AccountsCredentialsStorageNotice =>
        new("AccountsCredentialsStorageNotice");
    public static TeachingTip CheckpointCategoriesTip => new("CheckpointCategoriesTip");

    /// <inheritdoc />
    public override string ToString()
    {
        return base.ToString();
    }
}
