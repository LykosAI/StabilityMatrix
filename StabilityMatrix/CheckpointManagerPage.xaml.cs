using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using StabilityMatrix.ViewModels;
using Wpf.Ui.Controls;

namespace StabilityMatrix;

public partial class CheckpointManagerPage : Page
{
    private readonly CheckpointManagerViewModel viewModel;
    public CheckpointManagerPage(CheckpointManagerViewModel viewModel)
    {
        this.viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }

    private void FolderCard_OnPreviewDrop(object sender, DragEventArgs e)
    {
         Debug.WriteLine($"PreviewDrop: {sender}, {e}");
         if (e.Data.GetDataPresent(DataFormats.FileDrop))
         {
             var files = e.Data.GetData(DataFormats.FileDrop) as string[];
             var firstFile = files?[0];
             // Make title by title casing the file name
                var title = System.IO.Path.GetFileNameWithoutExtension(firstFile);
                title = Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(title!);
             viewModel.CheckpointFolders[0].CheckpointFiles.Add(new()
             {
                 Title = title,
                 FileName = firstFile!,
             });
         }
    }

    private void FolderCard_OnDrop(object sender, DragEventArgs e)
    {
        Debug.WriteLine($"Drop: {sender}, {e}");
    }

    private void FolderCard_OnPreviewDragOver(object sender, DragEventArgs e)
    {
        Debug.WriteLine($"PreviewDragOver: {sender}, {e}");
    }

    private void FolderCard_OnPreviewDragLeave(object sender, DragEventArgs e)
    {
        var senderCard = (CardExpander) sender;
        senderCard.Header = "Stable Diffusion";
        Debug.WriteLine($"PreviewDragLeave: {sender}, {e}");
    }

    private void FolderCard_OnPreviewDragEnter(object sender, DragEventArgs e)
    {
        var senderCard = (CardExpander) sender;
        senderCard.Header = "Drag here to add a checkpoint";
        // Apply a hover-over effect
        senderCard.Effect = new DropShadowEffect
        {
            Color = Colors.Black,
            Direction = 0,
            ShadowDepth = 0,
            Opacity = 0.5,
            BlurRadius = 10
        };
        Debug.WriteLine($"PreviewDragEnter: {sender}, {e}");
    }

    private async void CheckpointManagerPage_OnLoaded(object sender, RoutedEventArgs e)
    {
        await viewModel.OnLoaded();
    }
}
