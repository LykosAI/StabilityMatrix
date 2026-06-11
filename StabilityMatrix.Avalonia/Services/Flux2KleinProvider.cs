using Microsoft.Extensions.Logging;
using StabilityMatrix.Avalonia.Models.BananaVision;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;
using StabilityMatrix.Core.Services.ImageGeneration;

namespace StabilityMatrix.Avalonia.Services;

/// <summary>
/// Image generation provider for Flux.2 Klein using local ComfyUI backend.
/// Klein 4B is Apache 2.0 licensed; the distilled variant runs at 4 steps with CFG=1
/// making it well-suited to conversational, iterative editing.
/// </summary>
public class Flux2KleinProvider(ILogger<Flux2KleinProvider> logger, IInferenceClientManager clientManager)
    : ComfyImageGenerationProviderBase(logger, clientManager)
{
    public override string ProviderId => BananaVisionProviderIds.Flux2Klein;
    public override string ProviderName => "Flux.2 Klein (Local)";

    protected override string LogName => "Flux.2 Klein";

    // Klein supports multi-reference editing; cap at 4 for predictable VRAM use.
    protected override int MaxInputImages => 4;
    protected override string ProviderPrefix => "flux2_klein";

    protected override IReadOnlyList<string> GetMissingModels(ImageGenerationRequest request)
    {
        // Resolve the user's UNET selection first — the availability check is variant-aware
        // (a 9B UNET needs the qwen_3_8b encoder, 4B needs qwen_3_4b), so it has to know which
        // UNET the workflow will actually use. Logging happens on the build pass instead.
        var customUnetModel = GetCustomUnetModel(request, logSelection: false);

        var modelManager = new Flux2KleinModelManager();
        if (modelManager.AreModelsAvailable(ClientManager, customUnetModel))
        {
            return [];
        }
        return modelManager.GetMissingModelNames(ClientManager, customUnetModel).ToList();
    }

    protected override Dictionary<string, ComfyNode> BuildWorkflow(ImageGenerationRequest request)
    {
        var customUnetModel = GetCustomUnetModel(request);
        var loras = GetSelectedLoras(request);
        var (width, height) = GetDimensions(request);

        int? steps = null;
        double? cfg = null;
        var explicitDimensions = false;

        if (request.ProviderOptions != null)
        {
            if (
                request.ProviderOptions.TryGetValue("ExplicitDimensions", out var explicitObj)
                && explicitObj is bool eb
            )
            {
                explicitDimensions = eb;
            }

            if (request.ProviderOptions.TryGetValue("Steps", out var stepsObj) && stepsObj is int s)
            {
                steps = s;
            }

            if (request.ProviderOptions.TryGetValue("CfgScale", out var cfgObj))
            {
                cfg = cfgObj switch
                {
                    double d => d,
                    float f => f,
                    int i => i,
                    _ => null,
                };
            }

            if (steps.HasValue || cfg.HasValue)
            {
                Logger.LogInformation("Using Klein overrides: Steps={Steps}, Cfg={Cfg}", steps, cfg);
            }
        }

        return Flux2KleinWorkflowBuilder.Build(
            request,
            ClientManager,
            customUnetModel,
            loras,
            width,
            height,
            steps,
            cfg,
            explicitDimensions
        );
    }
}
