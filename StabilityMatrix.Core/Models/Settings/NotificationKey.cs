using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using StabilityMatrix.Core.Converters.Json;

namespace StabilityMatrix.Core.Models.Settings;

/// <summary>
/// Notification Names
/// </summary>
[SuppressMessage("ReSharper", "InconsistentNaming")]
[JsonConverter(typeof(StringJsonConverter<NotificationKey>))]
public record NotificationKey(string Value) : StringValue(Value)
{
    public NotificationOption DefaultOption { get; init; }

    public string? DisplayName { get; init; }

    public static NotificationKey Inference_PromptCompleted =>
        new("Inference_PromptCompleted")
        {
            DefaultOption = NotificationOption.NativePush,
            DisplayName = "Inference Prompt Completed"
        };

    public static NotificationKey Download_Completed =>
        new("Download_Completed")
        {
            DefaultOption = NotificationOption.NativePush,
            DisplayName = "Download Completed"
        };

    public static NotificationKey Download_Failed =>
        new("Download_Failed")
        {
            DefaultOption = NotificationOption.NativePush,
            DisplayName = "Download Failed"
        };

    public static NotificationKey Download_Canceled =>
        new("Download_Canceled")
        {
            DefaultOption = NotificationOption.NativePush,
            DisplayName = "Download Canceled"
        };

    public static Dictionary<string, NotificationKey> All { get; } = GetValues<NotificationKey>();

    /// <inheritdoc />
    public override string ToString() => base.ToString();
}
