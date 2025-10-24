using System.Linq;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;
using StabilityMatrix.Core.Models.Api.Comfy.NodeTypes;
using StabilityMatrix.Core.Models.Inference;

namespace StabilityMatrix.Avalonia.ViewModels.Inference.Modules;

[ManagedService]
[RegisterTransient<CfzCudnnToggleModule>]
public class CfzCudnnToggleModule : ModuleBase
{
    /// <inheritdoc />
    public CfzCudnnToggleModule(IServiceManager<ViewModelBase> vmFactory)
        : base(vmFactory)
    {
        Title = "CFZ CUDNN Toggle";
        AddCards(vmFactory.Get<CfzCudnnToggleCardViewModel>());
    }

    /// <summary>
    /// Applies CUDNN Toggle to the Model and Conditioning connections
    /// </summary>
    protected override void OnApplyStep(ModuleApplyStepEventArgs e)
    {
        var card = GetCard<CfzCudnnToggleCardViewModel>();

        // Apply to all models in the pipeline
        foreach (var modelConnections in e.Builder.Connections.Models.Values.Where(m => m.Model is not null))
        {
            var cudnnToggleOutput = e.Nodes.AddTypedNode(
                new ComfyNodeBuilder.CUDNNToggleAutoPassthrough
                {
                    Name = e.Nodes.GetUniqueName($"CUDNNToggle_{modelConnections.Name}"),
                    Model = modelConnections.Model,
                    Conditioning = modelConnections.Conditioning?.Positive,
                    Latent = null, // Optional, we're not using latent passthrough here
                    EnableCudnn = card.EnableCudnn,
                    CudnnBenchmark = card.CudnnBenchmark,
                }
            );

            // Update the model connection with the output from CUDNN toggle
            modelConnections.Model = cudnnToggleOutput.Output1;

            // Update conditioning if it was provided
            if (modelConnections.Conditioning is not null)
            {
                modelConnections.Conditioning = new ConditioningConnections(
                    cudnnToggleOutput.Output2,
                    modelConnections.Conditioning.Negative
                );
            }
        }
    }
}
