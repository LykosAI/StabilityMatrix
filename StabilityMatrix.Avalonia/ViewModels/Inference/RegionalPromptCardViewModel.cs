using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Injectio.Attributes;
using SkiaSharp;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(RegionalPromptCard))]
[ManagedService]
[RegisterTransient<RegionalPromptCardViewModel>]
public partial class RegionalPromptCardViewModel : LoadableViewModelBase
{
    public const string ModuleKey = "RegionalPrompt";

    private readonly IServiceManager<ViewModelBase> vmFactory;

    /// <summary>
    /// The layered mask editor for painting regions.
    /// Each layer = one prompt with its own mask.
    /// </summary>
    [JsonIgnore]
    public LayeredMaskEditorViewModel LayeredMaskEditor { get; }

    /// <summary>
    /// Convenience accessor for layers (for UI binding).
    /// </summary>
    [JsonIgnore]
    public ObservableCollection<MaskLayer> Layers => LayeredMaskEditor.Layers;

    public RegionalPromptCardViewModel(IServiceManager<ViewModelBase> vmFactory)
    {
        this.vmFactory = vmFactory;
        LayeredMaskEditor = vmFactory.Get<LayeredMaskEditorViewModel>();
    }

    /// <summary>
    /// Sets the canvas size for the mask editor.
    /// Should be called when the sampler dimensions change.
    /// </summary>
    public void SetCanvasSize(int width, int height)
    {
        LayeredMaskEditor.CanvasSize = new System.Drawing.Size(width, height);
    }

    /// <summary>
    /// Opens the layered mask editor dialog.
    /// </summary>
    [RelayCommand]
    private async Task OpenMaskEditorAsync()
    {
        // Ensure canvas size is set
        if (LayeredMaskEditor.CanvasSize == System.Drawing.Size.Empty)
        {
            LayeredMaskEditor.CanvasSize = new System.Drawing.Size(1024, 1024);
        }

        var dialog = LayeredMaskEditor.GetDialog();
        await dialog.ShowAsync();

        // Save current layer paths after dialog closes
        LayeredMaskEditor.SaveCurrentLayerPaths();
    }

    /// <summary>
    /// Gets enabled layers with content for generation.
    /// </summary>
    public IReadOnlyList<MaskLayer> GetEnabledLayersWithContent()
    {
        return LayeredMaskEditor.GetEnabledLayersWithContent();
    }

    /// <summary>
    /// Renders a layer to a mask image for ComfyUI.
    /// </summary>
    public SKImage? RenderLayerToMask(MaskLayer layer)
    {
        return LayeredMaskEditor.RenderLayerToMask(layer);
    }

    /// <inheritdoc />
    public override void LoadStateFromJsonObject(JsonObject state)
    {
        base.LoadStateFromJsonObject(state);

        // Load layered mask editor state
        if (
            state.TryGetPropertyValue("layeredMaskEditor", out var editorNode)
            && editorNode is JsonObject editorObj
        )
        {
            LayeredMaskEditor.LoadStateFromJsonObject(editorObj);
        }
    }

    /// <inheritdoc />
    public override JsonObject SaveStateToJsonObject()
    {
        var state = base.SaveStateToJsonObject();

        // Save layered mask editor state
        state["layeredMaskEditor"] = LayeredMaskEditor.SaveStateToJsonObject();

        return state;
    }
}
