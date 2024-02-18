using StabilityMatrix.Core.Models.FileInterfaces;

namespace StabilityMatrix.Avalonia.Models.TreeFileExplorer;

public class TreeFileExplorerFile(IPathObject path, TreeFileExplorerOptions options)
    : TreeFileExplorerItem(path, options);
