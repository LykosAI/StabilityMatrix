using Avalonia;
using Avalonia.Controls.Primitives;
using StabilityMatrix.Avalonia.Models.TreeFileExplorer;
using StabilityMatrix.Core.Models.FileInterfaces;

namespace StabilityMatrix.Avalonia.Controls;

public class TreeFileExplorer : TemplatedControlBase
{
    public static readonly StyledProperty<TreeFileExplorerDirectory?> RootItemProperty =
        AvaloniaProperty.Register<TreeFileExplorer, TreeFileExplorerDirectory?>("RootItem");

    public TreeFileExplorerDirectory? RootItem
    {
        get => GetValue(RootItemProperty);
        set => SetValue(RootItemProperty, value);
    }

    public static readonly StyledProperty<string?> RootPathProperty = AvaloniaProperty.Register<
        TreeFileExplorer,
        string?
    >("RootPath");

    public string? RootPath
    {
        get => GetValue(RootPathProperty);
        set => SetValue(RootPathProperty, value);
    }

    public static readonly StyledProperty<IPathObject?> SelectedPathProperty = AvaloniaProperty.Register<
        TreeFileExplorer,
        IPathObject?
    >("SelectedPath");

    public IPathObject? SelectedPath
    {
        get => GetValue(SelectedPathProperty);
        set => SetValue(SelectedPathProperty, value);
    }

    public static readonly StyledProperty<bool> CanSelectFilesProperty = AvaloniaProperty.Register<
        TreeFileExplorer,
        bool
    >("CanSelectFiles", true);

    public bool CanSelectFiles
    {
        get => GetValue(CanSelectFilesProperty);
        set => SetValue(CanSelectFilesProperty, value);
    }

    public static readonly StyledProperty<bool> CanSelectFoldersProperty = AvaloniaProperty.Register<
        TreeFileExplorer,
        bool
    >("CanSelectFolders", true);

    public bool CanSelectFolders
    {
        get => GetValue(CanSelectFoldersProperty);
        set => SetValue(CanSelectFoldersProperty, value);
    }

    public static readonly StyledProperty<bool> IndexFilesProperty = AvaloniaProperty.Register<
        TreeFileExplorer,
        bool
    >("IndexFiles", true);

    public bool IndexFiles
    {
        get => GetValue(IndexFilesProperty);
        set => SetValue(IndexFilesProperty, value);
    }

    public static readonly StyledProperty<bool> IndexFoldersProperty = AvaloniaProperty.Register<
        TreeFileExplorer,
        bool
    >("IndexFolders", true);

    public bool IndexFolders
    {
        get => GetValue(IndexFoldersProperty);
        set => SetValue(IndexFoldersProperty, value);
    }

    private TreeFileExplorerOptions GetOptions()
    {
        var options = TreeFileExplorerOptions.None;

        if (CanSelectFiles)
        {
            options |= TreeFileExplorerOptions.CanSelectFiles;
        }
        if (CanSelectFolders)
        {
            options |= TreeFileExplorerOptions.CanSelectFolders;
        }
        if (IndexFiles)
        {
            options |= TreeFileExplorerOptions.IndexFiles;
        }
        if (IndexFolders)
        {
            options |= TreeFileExplorerOptions.IndexFolders;
        }

        return options;
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (RootItem is null)
        {
            RootItem = RootPath is null
                ? null
                : new TreeFileExplorerDirectory(new DirectoryPath(RootPath), GetOptions());
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == RootPathProperty)
        {
            var path = change.GetNewValue<string?>();
            RootItem = path is null
                ? null
                : new TreeFileExplorerDirectory(new DirectoryPath(path), GetOptions());
        }
    }
}
