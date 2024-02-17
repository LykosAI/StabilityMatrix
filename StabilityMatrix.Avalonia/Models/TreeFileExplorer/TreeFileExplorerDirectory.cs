using System;
using System.Collections.Generic;
using System.Linq;
using StabilityMatrix.Core.Models.FileInterfaces;

namespace StabilityMatrix.Avalonia.Models.TreeFileExplorer;

public class TreeFileExplorerDirectory(IPathObject path, TreeFileExplorerOptions options)
    : TreeFileExplorerItem(path, options)
{
    public IEnumerable<TreeFileExplorerItem> Children =>
        GetChildren(Path, Options)
            .OrderByDescending(item => item.Path is DirectoryPath)
            .ThenBy(item => item.Path.Name);

    private static IEnumerable<TreeFileExplorerItem> GetChildren(
        IPathObject pathObject,
        TreeFileExplorerOptions options
    )
    {
        return pathObject switch
        {
            FilePath => Enumerable.Empty<TreeFileExplorerItem>(),
            DirectoryPath directoryPath => GetChildren(directoryPath, options),
            _ => throw new NotSupportedException()
        };
    }

    private static IEnumerable<TreeFileExplorerItem> GetChildren(
        DirectoryPath directoryPath,
        TreeFileExplorerOptions options
    )
    {
        if (options.HasFlag(TreeFileExplorerOptions.IndexFiles))
        {
            foreach (var file in directoryPath.EnumerateFiles())
            {
                yield return new TreeFileExplorerFile(file, options);
            }
        }

        if (options.HasFlag(TreeFileExplorerOptions.IndexFolders))
        {
            foreach (var directory in directoryPath.EnumerateDirectories())
            {
                yield return new TreeFileExplorerDirectory(directory, options);
            }
        }
    }
}
