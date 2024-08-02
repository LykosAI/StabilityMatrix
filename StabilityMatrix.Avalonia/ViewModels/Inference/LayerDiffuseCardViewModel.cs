using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using KGySoft.CoreLibraries;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;
using StabilityMatrix.Core.Models.Inference;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[Transient]
[ManagedService]
[View(typeof(LayerDiffuseCard))]
public partial class LayerDiffuseCardViewModel : LoadableViewModelBase, IComfyStep
{
    public const string ModuleKey = "LayerDiffuse";

    [ObservableProperty]
    private LayerDiffuseMode selectedMode = LayerDiffuseMode.None;

    public IEnumerable<LayerDiffuseMode> AvailableModes => Enum<LayerDiffuseMode>.GetValues();

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required]
    [Range(-1d, 3d)]
    private double weight = 1;

    /// <inheritdoc />
    public void ApplyStep(ModuleApplyStepEventArgs e)
    {
        if (SelectedMode == LayerDiffuseMode.None)
            return;

        var sdType = SelectedMode switch
        {
            LayerDiffuseMode.GenerateForegroundWithTransparencySD15 => "SD15",
            LayerDiffuseMode.GenerateForegroundWithTransparencySDXL => "SDXL",
            LayerDiffuseMode.None => throw new ArgumentOutOfRangeException(),
            _ => throw new ArgumentOutOfRangeException()
        };

        // Choose config based on mode
        var config = SelectedMode switch
        {
            LayerDiffuseMode.GenerateForegroundWithTransparencySD15
                => "SD15, Attention Injection, attn_sharing",
            LayerDiffuseMode.GenerateForegroundWithTransparencySDXL => "SDXL, Conv Injection",
            LayerDiffuseMode.None => throw new ArgumentOutOfRangeException(),
            _ => throw new ArgumentOutOfRangeException()
        };

        foreach (var modelConnections in e.Temp.Models.Values)
        {
            var layerDiffuseApply = e.Nodes.AddTypedNode(
                new ComfyNodeBuilder.LayeredDiffusionApply
                {
                    Name = e.Nodes.GetUniqueName($"LayerDiffuseApply_{modelConnections.Name}"),
                    Model = modelConnections.Model,
                    Config = config,
                    Weight = Weight,
                }
            );

            modelConnections.Model = layerDiffuseApply.Output;
        }

        // Add pre output action
        e.PreOutputActions.Add(applyArgs =>
        {
            // Use last latent for decode
            var latent =
                applyArgs.Builder.Connections.LastPrimaryLatent
                ?? throw new InvalidOperationException("Connections.LastPrimaryLatent not set");

            // Convert primary to image if not already
            var primaryImage = applyArgs.Builder.GetPrimaryAsImage();
            applyArgs.Builder.Connections.Primary = primaryImage;

            // Add a Layer Diffuse Decode
            var decode = applyArgs.Nodes.AddTypedNode(
                new ComfyNodeBuilder.LayeredDiffusionDecodeRgba
                {
                    Name = applyArgs.Nodes.GetUniqueName("LayerDiffuseDecode"),
                    Samples = latent,
                    Images = primaryImage,
                    SdVersion = sdType
                }
            );

            // Set primary to decode output
            applyArgs.Builder.Connections.Primary = decode.Output;
        });
    }
}
