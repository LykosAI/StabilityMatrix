using System.Diagnostics.CodeAnalysis;

namespace StabilityMatrix.Core.ReparsePoints;

[Flags]
[SuppressMessage("ReSharper", "InconsistentNaming")]
internal enum Win32FileAttribute : uint
{
    AttributeReadOnly = 0x1U,
    AttributeHidden = 0x2U,
    AttributeSystem = 0x4U,
    AttributeDirectory = 0x10U,
    AttributeArchive = 0x20U,
    AttributeDevice = 0x40U,
    AttributeNormal = 0x80U,
    AttributeTemporary = 0x100U,
    AttributeSparseFile = 0x200U,
    AttributeReparsePoint = 0x400U,
    AttributeCompressed = 0x800U,
    AttributeOffline = 0x1000U,
    AttributeNotContentIndexed = 0x2000U,
    AttributeEncrypted = 0x4000U,
    AttributeIntegrityStream = 0x8000U,
    AttributeVirtual = 0x10000U,
    AttributeNoScrubData = 0x20000U,
    AttributeEA = 0x40000U,
    AttributeRecallOnOpen = 0x40000U,
    AttributePinned = 0x80000U,
    AttributeUnpinned = 0x100000U,
    AttributeRecallOnDataAccess = 0x400000U,
    FlagOpenNoRecall = 0x100000U,
    /// <summary>
    /// Normal reparse point processing will not occur; CreateFile will attempt to open the reparse point. When a file is opened, a file handle is returned,
    /// whether or not the filter that controls the reparse point is operational.
    /// <br />This flag cannot be used with the <see cref="FileMode.Create"/> flag.
    /// <br />If the file is not a reparse point, then this flag is ignored.
    /// </summary>
    FlagOpenReparsePoint = 0x200000U,
    FlagSessionAware = 0x800000U,
    FlagPosixSemantics = 0x1000000U,
    /// <summary>
    /// You must set this flag to obtain a handle to a directory. A directory handle can be passed to some functions instead of a file handle.
    /// </summary>
    FlagBackupSemantics = 0x2000000U,
    FlagDeleteOnClose = 0x4000000U,
    FlagSequentialScan = 0x8000000U,
    FlagRandomAccess = 0x10000000U,
    FlagNoBuffering = 0x20000000U,
    FlagOverlapped = 0x40000000U,
    FlagWriteThrough = 0x80000000U
}
