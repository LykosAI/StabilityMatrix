using System.Diagnostics.CodeAnalysis;

namespace StabilityMatrix.Native.Windows.Interop;

[Flags]
[SuppressMessage("ReSharper", "InconsistentNaming")]
[SuppressMessage("ReSharper", "IdentifierTypo")]
internal enum FileOperationFlags : uint
{
    FOF_MULTIDESTFILES = 0x0001,
    FOF_CONFIRMMOUSE = 0x0002,
    FOF_WANTMAPPINGHANDLE = 0x0020, // Fill in SHFILEOPSTRUCT.hNameMappings
    FOF_FILESONLY = 0x0080, // on *.*, do only files
    FOF_NOCONFIRMMKDIR = 0x0200, // don't confirm making any needed dirs
    FOF_NOCOPYSECURITYATTRIBS = 0x0800, // dont copy NT file Security Attributes
    FOF_NORECURSION = 0x1000, // don't recurse into directories.
    FOF_NO_CONNECTED_ELEMENTS = 0x2000, // don't operate on connected file elements.
    FOF_NORECURSEREPARSE = 0x8000, // treat reparse points as objects, not containers

    /// <summary>
    /// Do not show a dialog during the process
    /// </summary>
    FOF_SILENT = 0x0004,

    FOF_RENAMEONCOLLISION = 0x0008,

    /// <summary>
    /// Do not ask the user to confirm selection
    /// </summary>
    FOF_NOCONFIRMATION = 0x0010,

    /// <summary>
    /// Delete the file to the recycle bin.  (Required flag to send a file to the bin
    /// </summary>
    FOF_ALLOWUNDO = 0x0040,

    /// <summary>
    /// Do not show the names of the files or folders that are being recycled.
    /// </summary>
    FOF_SIMPLEPROGRESS = 0x0100,

    /// <summary>
    /// Surpress errors, if any occur during the process.
    /// </summary>
    FOF_NOERRORUI = 0x0400,

    /// <summary>
    /// Warn if files are too big to fit in the recycle bin and will need
    /// to be deleted completely.
    /// </summary>
    FOF_WANTNUKEWARNING = 0x4000,

    FOFX_ADDUNDORECORD = 0x20000000,

    FOFX_NOSKIPJUNCTIONS = 0x00010000,

    FOFX_PREFERHARDLINK = 0x00020000,

    FOFX_SHOWELEVATIONPROMPT = 0x00040000,

    FOFX_EARLYFAILURE = 0x00100000,

    FOFX_PRESERVEFILEEXTENSIONS = 0x00200000,

    FOFX_KEEPNEWERFILE = 0x00400000,

    FOFX_NOCOPYHOOKS = 0x00800000,

    FOFX_NOMINIMIZEBOX = 0x01000000,

    FOFX_MOVEACLSACROSSVOLUMES = 0x02000000,

    FOFX_DONTDISPLAYSOURCEPATH = 0x04000000,

    FOFX_DONTDISPLAYDESTPATH = 0x08000000,

    FOFX_RECYCLEONDELETE = 0x00080000,

    FOFX_REQUIREELEVATION = 0x10000000,

    FOFX_COPYASDOWNLOAD = 0x40000000,

    FOFX_DONTDISPLAYLOCATIONS = 0x80000000,
}
