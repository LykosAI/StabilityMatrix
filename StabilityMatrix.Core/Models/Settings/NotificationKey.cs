using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using StabilityMatrix.Core.Converters.Json;
using StabilityMatrix.Core.Helper;

namespace StabilityMatrix.Core.Models.Settings;

/// <summary>
/// Notification Names
/// </summary>
[SuppressMessage("ReSharper", "InconsistentNaming")]
[JsonConverter(typeof(ParsableStringValueJsonConverter<NotificationKey>))]
public record NotificationKey(string Value) : StringValue(Value), IParsable<NotificationKey>
{
    public NotificationOption DefaultOption { get; init; }

    public NotificationLevel Level { get; init; }

    public string? DisplayName { get; init; }

    public static NotificationKey Inference_PromptCompleted =>
        new("Inference_PromptCompleted")
        {
            DefaultOption = Compat.IsLinux ? NotificationOption.AppToast : NotificationOption.NativePush,
            Level = NotificationLevel.Success,
            DisplayName = "Inference Prompt Completed"
        };

    public static NotificationKey Download_Completed =>
        new("Download_Completed")
        {
            DefaultOption = Compat.IsLinux ? NotificationOption.AppToast : NotificationOption.NativePush,
            Level = NotificationLevel.Success,
            DisplayName = "Download Completed"
        };

    public static NotificationKey Download_Failed =>
        new("Download_Failed")
        {
            DefaultOption = Compat.IsLinux ? NotificationOption.AppToast : NotificationOption.NativePush,
            Level = NotificationLevel.Error,
            DisplayName = "Download Failed"
        };

    public static NotificationKey Download_Canceled =>
        new("Download_Canceled")
        {
            DefaultOption = Compat.IsLinux ? NotificationOption.AppToast : NotificationOption.NativePush,
            Level = NotificationLevel.Warning,
            DisplayName = "Download Canceled"
        };

    public static NotificationKey Package_Install_Completed =>
        new("Package_Install_Completed")
        {
            DefaultOption = Compat.IsLinux ? NotificationOption.AppToast : NotificationOption.NativePush,
            Level = NotificationLevel.Success,
            DisplayName = "Package Install Completed"
        };

    public static NotificationKey Package_Install_Failed =>
        new("Package_Install_Failed")
        {
            DefaultOption = Compat.IsLinux ? NotificationOption.AppToast : NotificationOption.NativePush,
            Level = NotificationLevel.Error,
            DisplayName = "Package Install Failed"
        };

    public static Dictionary<string, NotificationKey> All { get; } = GetValues<NotificationKey>();

    /// <inheritdoc />
    public override string ToString() => base.ToString();

    /// <inheritdoc />
    public static NotificationKey Parse(string s, IFormatProvider? provider)
    {
        return All[s];
    }

    /// <inheritdoc />
    public static bool TryParse(
        string? s,
        IFormatProvider? provider,
        [MaybeNullWhen(false)] out NotificationKey result
    )
    {
        return All.TryGetValue(s ?? "", out result);
    }
}
