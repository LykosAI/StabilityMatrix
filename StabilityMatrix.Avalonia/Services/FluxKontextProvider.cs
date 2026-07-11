using Microsoft.Extensions.Logging;
using StabilityMatrix.Avalonia.Models.BananaVision;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;
using StabilityMatrix.Core.Services.ImageGeneration;

namespace StabilityMatrix.Avalonia.Services;

/// <summary>
/// Image generation provider for Flux Kontext using local ComfyUI backend
/// </summary>
public class FluxKontextProvider(ILogger<FluxKontextProvider> logger, IInferenceClientManager clientManager)
    : ComfyImageGenerationProviderBase(logger, clientManager)
{
    public override string ProviderId => BananaVisionProviderIds.FluxKontext;
    public override string ProviderName => "Flux Kontext (Local)";

    protected override string LogName => "Flux Kontext";
    protected override int MaxInputImages => 2;
    protected override string ProviderPrefix => "flux_kontext";

    protected override IReadOnlyList<string> GetMissingModels(ImageGenerationRequest request)
    {
        var modelManager = new FluxKontextModelManager();
        if (modelManager.AreModelsAvailable(ClientManager))
        {
            return [];
        }
        return modelManager.GetMissingModelNames(ClientManager).ToList();
    }

    protected override Dictionary<string, ComfyNode> BuildWorkflow(ImageGenerationRequest request)
    {
        var customUnetModel = GetCustomUnetModel(request);
        var loras = GetSelectedLoras(request);
        var (width, height) = GetDimensions(request);

        return FluxKontextWorkflowBuilder.Build(
            request,
            ClientManager,
            customUnetModel,
            loras,
            width,
            height
        );
    }
}
