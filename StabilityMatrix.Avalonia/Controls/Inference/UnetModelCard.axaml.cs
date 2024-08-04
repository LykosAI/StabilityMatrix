using AsyncAwaitBestPractices;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using StabilityMatrix.Avalonia.ViewModels.Inference;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Avalonia.Controls;

[Transient]
public class UnetModelCard : TemplatedControl
{
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        var clip1ComboBox = e.NameScope.Find("Clip1ComboBox") as BetterComboBox;
        clip1ComboBox!.SelectionChanged += UpscalerComboBox_OnSelectionChanged;

        var clip2ComboBox = e.NameScope.Find("Clip2ComboBox") as BetterComboBox;
        clip2ComboBox!.SelectionChanged += UpscalerComboBox_OnSelectionChanged;
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
            (DataContext as UnetModelCardViewModel)!
                .RemoteDownloadCommand.ExecuteAsync(item)
                .SafeFireAndForget();
        }
    }
}
