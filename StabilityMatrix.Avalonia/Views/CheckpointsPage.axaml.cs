using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Core.Attributes;
using CheckpointFolder = StabilityMatrix.Avalonia.ViewModels.CheckpointManager.CheckpointFolder;

namespace StabilityMatrix.Avalonia.Views;

[Singleton]
public partial class CheckpointsPage : UserControlBase
{
    public CheckpointsPage()
    {
        InitializeComponent();

        AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        AddHandler(DragDrop.DragLeaveEvent, OnDragExit);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private static async void OnDrop(object? sender, DragEventArgs e)
    {
        var sourceDataContext = (e.Source as Control)?.DataContext;
        if (sourceDataContext is CheckpointFolder folder)
        {
            await folder.OnDrop(e);
        }
    }

    private static void OnDragExit(object? sender, DragEventArgs e)
    {
        var sourceDataContext = (e.Source as Control)?.DataContext;
        if (sourceDataContext is CheckpointFolder folder)
        {
            folder.IsCurrentDragTarget = false;
        }
    }

    private static void OnDragEnter(object? sender, DragEventArgs e)
    {
        // Only allow Copy or Link as Drop Operations.
        e.DragEffects &= DragDropEffects.Copy | DragDropEffects.Link;

        // Only allow if the dragged data contains text or filenames.
        if (!e.Data.Contains(DataFormats.Text) && !e.Data.Contains(DataFormats.Files))
        {
            e.DragEffects = DragDropEffects.None;
        }

        // Forward to view model
        var sourceDataContext = (e.Source as Control)?.DataContext;
        if (sourceDataContext is CheckpointFolder folder)
        {
            folder.IsCurrentDragTarget = true;
        }
    }
}
