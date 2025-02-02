using System;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference.Modules;

[ManagedService]
[RegisterTransient<PromptExpansionModule>]
public class PromptExpansionModule : ModuleBase
{
    public PromptExpansionModule(ServiceManager<ViewModelBase> vmFactory)
        : base(vmFactory)
    {
        Title = "Prompt Expansion";
        AddCards(vmFactory.Get<PromptExpansionCardViewModel>());
    }

    protected override void OnApplyStep(ModuleApplyStepEventArgs e)
    {
        var promptExpansionCard = GetCard<PromptExpansionCardViewModel>();

        var model =
            promptExpansionCard.SelectedModel
            ?? throw new InvalidOperationException($"{Title}: Model not selected");

        e.Builder.Connections.PositivePrompt = e.Nodes.AddTypedNode(
            new ComfyNodeBuilder.PromptExpansion
            {
                Name = e.Nodes.GetUniqueName("PromptExpansion_Positive"),
                ModelName = model.RelativePath,
                Text = e.Builder.Connections.PositivePrompt,
                Seed = e.Builder.Connections.Seed,
                LogPrompt = promptExpansionCard.IsLogOutputEnabled
            }
        ).Output;
    }
}
