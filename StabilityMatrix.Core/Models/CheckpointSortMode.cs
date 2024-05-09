using StabilityMatrix.Core.Extensions;

namespace StabilityMatrix.Core.Models;

public enum CheckpointSortMode
{
    [StringValue("File Name")]
    FileName,

    [StringValue("Title")]
    Title,

    [StringValue("Base Model")]
    BaseModel,

    [StringValue("Type")]
    SharedFolderType
}
