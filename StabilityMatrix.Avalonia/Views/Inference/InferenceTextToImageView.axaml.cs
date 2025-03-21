using Avalonia;
using Avalonia.Controls;
using Dock.Avalonia.Controls;
using Dock.Model.Avalonia.Controls;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls.Dock;

namespace StabilityMatrix.Avalonia.Views.Inference;

[RegisterTransient<InferenceTextToImageView>]
public partial class InferenceTextToImageView : DockUserControlBase
{
    private bool hasMovedLoraDock = false;

    public InferenceTextToImageView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // TODO: this
        // get LoraGallery ToolDock
        // var dock = this.FindControl<DockControl>("Dock");
        // var dockable = this.Find<Tool>("LoraGalleryTool");
        // if (dockable == null || hasMovedLoraDock)
        //     return;
        //
        // dock?.Factory?.PinDockable(dockable);
        // hasMovedLoraDock = true;
    }
}
