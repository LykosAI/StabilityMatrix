using System.Text.Json.Serialization;
using StabilityMatrix.Core.Converters.Json;

namespace StabilityMatrix.Core.Models.Settings;

/// <summary>
/// Teaching tip names
/// </summary>
[JsonConverter(typeof(StringJsonConverter<TeachingTip>))]
public record TeachingTip(string Value) : StringValue(Value)
{
    public static TeachingTip AccountsCredentialsStorageNotice => new("AccountsCredentialsStorageNotice");
    public static TeachingTip CheckpointCategoriesTip => new("CheckpointCategoriesTip");
    public static TeachingTip PackageExtensionsInstallNotice => new("PackageExtensionsInstallNotice");
    public static TeachingTip DownloadsTip => new("DownloadsTip");
    public static TeachingTip WebUiButtonMovedTip => new("WebUiButtonMovedTip");
    public static TeachingTip InferencePromptHelpButtonTip => new("InferencePromptHelpButtonTip");
    public static TeachingTip LykosAccountMigrateTip => new("LykosAccountMigrateTip");
    public static TeachingTip SharedFolderMigrationTip => new("SharedFolderMigrationTip");
    public static TeachingTip FolderMapTip => new("FolderMapTip");

    /// <inheritdoc />
    public override string ToString()
    {
        return base.ToString();
    }
}
