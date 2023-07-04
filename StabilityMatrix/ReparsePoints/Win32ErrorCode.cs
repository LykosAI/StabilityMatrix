using StabilityMatrix.Extensions;

namespace StabilityMatrix.ReparsePoints;

internal enum Win32ErrorCode
{
    /// <summary>
    /// The file or directory is not a reparse point.
    /// </summary>
    [StringValue("ERROR_NOT_A_REPARSE_POINT")]
    NotAReparsePoint = 4390,

    /// <summary>
    /// The reparse point attribute cannot be set because it conflicts with an existing attribute.
    /// </summary>
    [StringValue("ERROR_REPARSE_ATTRIBUTE_CONFLICT")]
    ReparseAttributeConflict = 4391,

    /// <summary>
    /// The data present in the reparse point buffer is invalid.
    /// </summary>
    [StringValue("ERROR_INVALID_REPARSE_DATA")]
    InvalidReparseData = 4392,

    /// <summary>
    /// The tag present in the reparse point buffer is invalid.
    /// </summary>
    [StringValue("ERROR_REPARSE_TAG_INVALID")]
    ReparseTagInvalid = 4393,

    /// <summary>
    /// There is a mismatch between the tag specified in the request and the tag present in the reparse point.
    /// </summary>
    [StringValue("ERROR_REPARSE_TAG_MISMATCH")]
    ReparseTagMismatch = 4394,
}
