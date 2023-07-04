namespace StabilityMatrix.ReparsePoints;

internal enum DeviceIoControlCode : uint
{
    /// <summary>
    /// FSCTL_SET_REPARSE_POINT
    /// Command to set the reparse point data block.
    /// </summary>
    SetReparsePoint = 0x000900A4,

    /// <summary>
    /// FSCTL_GET_REPARSE_POINT
    /// Command to get the reparse point data block.
    /// </summary>
    GetReparsePoint = 0x000900A8,

    /// <summary>
    /// FSCTL_DELETE_REPARSE_POINT
    /// Command to delete the reparse point data base.
    /// </summary>
    DeleteReparsePoint = 0x000900AC,

    /// <summary>
    /// IO_REPARSE_TAG_MOUNT_POINT
    /// Reparse point tag used to identify mount points and junction points.
    /// </summary>
    ReparseTagMountPoint = 0xA0000003,
}
