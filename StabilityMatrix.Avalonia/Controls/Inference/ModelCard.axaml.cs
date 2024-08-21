using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.Inference;

namespace StabilityMatrix.Avalonia.Controls;

[Transient]
public class ModelCard : TemplatedControl
{
    private BetterComboBox? modelComboBox;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        modelComboBox = e.NameScope.Find("PART_ModelComboBox") as BetterComboBox;

        if (e.NameScope.Find("PART_ModelLoaderComboBox") is BetterComboBox modelLoaderComboBox)
        {
            modelLoaderComboBox.SelectionChanged += ModelComboBoxOnSelectionChanged;
        }
    }

    private void ModelComboBoxOnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count <= 0)
            return;

        if (e.AddedItems[0] is ModelLoader modelLoader)
        {
            modelComboBox?.Bind(ItemsControl.ItemsSourceProperty, new Binding(GetBindingPath(modelLoader)));
        }
    }

    private string GetBindingPath(ModelLoader modelLoader)
    {
        return modelLoader switch
        {
            ModelLoader.Default => "ClientManager.Models",
            ModelLoader.Gguf => "ClientManager.UnetModels",
            ModelLoader.Nf4 => "ClientManager.Models",
            ModelLoader.Unet => "ClientManager.UnetModels",
            _ => throw new ArgumentOutOfRangeException(nameof(modelLoader), modelLoader, null)
        };
    }
}
