using AsyncAwaitBestPractices;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.ViewModels.Inference;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Avalonia.Controls;

[Transient]
public class PromptExpansionCard : TemplatedControl
{
    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        var upscalerComboBox = e.NameScope.Find("PART_ModelComboBox") as FAComboBox;
        upscalerComboBox!.SelectionChanged += UpscalerComboBox_OnSelectionChanged;
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
                (sender as FAComboBox)!.SelectedItem = removedItem;
            }
            else
            {
                (sender as FAComboBox)!.SelectedItem = null;
            }

            // Show dialog to download the model
            (DataContext as PromptExpansionCardViewModel)!
                .RemoteDownloadCommand.ExecuteAsync(item)
                .SafeFireAndForget();
        }
    }
}
