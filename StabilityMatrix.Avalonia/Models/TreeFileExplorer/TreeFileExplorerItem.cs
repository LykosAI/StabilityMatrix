using StabilityMatrix.Core.Models.FileInterfaces;

namespace StabilityMatrix.Avalonia.Models.TreeFileExplorer;

public class TreeFileExplorerItem(IPathObject path, TreeFileExplorerOptions options)
{
    public IPathObject Path { get; } = path;

    public TreeFileExplorerOptions Options { get; } = options;
}
