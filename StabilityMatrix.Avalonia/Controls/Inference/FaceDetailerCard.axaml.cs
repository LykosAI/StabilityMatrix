using AsyncAwaitBestPractices;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using StabilityMatrix.Avalonia.ViewModels.Inference;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Avalonia.Controls;

[Transient]
public class FaceDetailerCard : TemplatedControl
{
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        var bboxModelComboBox = e.NameScope.Find("PART_BboxModelComboBox") as BetterComboBox;
        bboxModelComboBox!.SelectionChanged += UpscalerComboBox_OnSelectionChanged;

        var segmModelComboBox = e.NameScope.Find("PART_SegmModelComboBox") as BetterComboBox;
        segmModelComboBox!.SelectionChanged += UpscalerComboBox_OnSelectionChanged;

        var samModelComboBox = e.NameScope.Find("PART_SamModelComboBox") as BetterComboBox;
        samModelComboBox!.SelectionChanged += UpscalerComboBox_OnSelectionChanged;
    }

    private void UpscalerComboBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0)
            return;

        var item = e.AddedItems[0];
        if (item is HybridModelFile { IsDownloadable: true })
        {
            // Reset the selection
            e.Handled = true;

            if (
                e.RemovedItems.Count > 0
                && e.RemovedItems[0] is HybridModelFile { IsDownloadable: false } removedItem
            )
            {
                (sender as BetterComboBox)!.SelectedItem = removedItem;
            }
            else
            {
                (sender as BetterComboBox)!.SelectedItem = null;
            }

            // Show dialog to download the model
            (DataContext as FaceDetailerViewModel)!
                .RemoteDownloadCommand.ExecuteAsync(item)
                .SafeFireAndForget();
        }
    }
}
