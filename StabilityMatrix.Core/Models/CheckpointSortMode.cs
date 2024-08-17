using StabilityMatrix.Core.Extensions;

namespace StabilityMatrix.Core.Models;

public enum CheckpointSortMode
{
    [StringValue("Base Model")]
    BaseModel,

    [StringValue("File Name")]
    FileName,

    [StringValue("File Size")]
    FileSize,

    [StringValue("Title")]
    Title,

    [StringValue("Type")]
    SharedFolderType,

    [StringValue("Update Available")]
    UpdateAvailable,
}
