namespace StabilityMatrix.Core.ReparsePoints;

internal enum Win32ErrorCode
{
    /// <summary>
    /// The file or directory is not a reparse point.
    /// ERROR_NOT_A_REPARSE_POINT
    /// </summary>
    NotAReparsePoint = 4390,

    /// <summary>
    /// The reparse point attribute cannot be set because it conflicts with an existing attribute.
    /// ERROR_REPARSE_ATTRIBUTE_CONFLICT
    /// </summary>
    ReparseAttributeConflict = 4391,

    /// <summary>
    /// The data present in the reparse point buffer is invalid.
    /// ERROR_INVALID_REPARSE_DATA
    /// </summary>
    InvalidReparseData = 4392,

    /// <summary>
    /// The tag present in the reparse point buffer is invalid.
    /// ERROR_REPARSE_TAG_INVALID
    /// </summary>
    ReparseTagInvalid = 4393,

    /// <summary>
    /// There is a mismatch between the tag specified in the request and the tag present in the reparse point.
    /// ERROR_REPARSE_TAG_MISMATCH
    /// </summary>
    ReparseTagMismatch = 4394,
}
