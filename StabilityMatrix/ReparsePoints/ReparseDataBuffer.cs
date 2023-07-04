using System.Runtime.InteropServices;

namespace StabilityMatrix.ReparsePoints;

/// <summary>
/// Because the tag we're using is IO_REPARSE_TAG_MOUNT_POINT,
/// we use the MountPointReparseBuffer struct in the DUMMYUNIONNAME union.
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct ReparseDataBuffer
{
    /// <summary>
    /// Reparse point tag. Must be a Microsoft reparse point tag.
    /// </summary>
    public uint ReparseTag;
    
    /// <summary>
    /// Size, in bytes, of the reparse data in the buffer that <see cref="PathBuffer"/> points to.
    /// This can be calculated by:
    /// (4 * sizeof(ushort)) + SubstituteNameLength + PrintNameLength + 
    /// (namesAreNullTerminated ? 2 * sizeof(char) : 0);
    /// </summary>
    public ushort ReparseDataLength;
    
    /// <summary>
    /// Reserved; do not use.
    /// </summary>
#pragma warning disable CS0169 // Field is never used
    private ushort Reserved;
#pragma warning restore CS0169 // Field is never used
    
    /// <summary>
    /// Offset, in bytes, of the substitute name string in the <see cref="PathBuffer"/> array.
    /// </summary>
    public ushort SubstituteNameOffset;
    
    /// <summary>
    /// Length, in bytes, of the substitute name string. If this string is null-terminated,
    /// <see cref="SubstituteNameLength"/> does not include space for the null character.
    /// </summary>
    public ushort SubstituteNameLength;
    
    /// <summary>
    /// Offset, in bytes, of the print name string in the <see cref="PathBuffer"/> array.
    /// </summary>
    public ushort PrintNameOffset;
    
    /// <summary>
    /// Length, in bytes, of the print name string. If this string is null-terminated,
    /// <see cref="PrintNameLength"/> does not include space for the null character.
    /// </summary>
    public ushort PrintNameLength;
    
    /// <summary>
    /// A buffer containing the unicode-encoded path string. The path string contains
    /// the substitute name string and print name string.
    /// </summary>
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x3FF0)]
    public byte[] PathBuffer;
}
