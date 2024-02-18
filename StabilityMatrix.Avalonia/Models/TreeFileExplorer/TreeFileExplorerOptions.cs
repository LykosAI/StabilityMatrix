using System;

namespace StabilityMatrix.Avalonia.Models.TreeFileExplorer;

[Flags]
public enum TreeFileExplorerOptions
{
    None = 0,

    IndexFiles = 1 << 5,
    IndexFolders = 1 << 6,

    CanSelectFiles = 1 << 10,
    CanSelectFolders = 1 << 11,
}
