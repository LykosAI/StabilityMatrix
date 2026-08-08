using Microsoft.Extensions.Logging;
using StabilityMatrix.Avalonia.Models.BananaVision;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;
using StabilityMatrix.Core.Services.ImageGeneration;

namespace StabilityMatrix.Avalonia.Services;

/// <summary>
/// Image generation provider for Qwen Image text-to-image using local ComfyUI backend.
/// Text-to-image only — image-conditioned editing is Qwen Image Edit's job (its own
/// provider), so this one doesn't accept reference images.
/// </summary>
public class QwenImageProvider(
    ILogger<QwenImageProvider> logger,
    IInferenceClientManager clientManager,
    RunningPackageService runningPackageService,
    INotificationService notificationService
) : ComfyImageGenerationProviderBase(logger, clientManager, runningPackageService, notificationService)
{
    public override string ProviderId => BananaVisionProviderIds.QwenImage;
    public override string ProviderName => "Qwen Image (Local)";

    public override bool SupportsImageInput => false;

    protected override string LogName => "Qwen Image";
    protected override int MaxInputImages => 0;
    protected override string ProviderPrefix => "qwen_image";

    protected override IReadOnlyList<string> GetMissingModels(ImageGenerationRequest request)
    {
        var modelManager = new QwenImageModelManager();
        if (modelManager.AreModelsAvailable(ClientManager))
        {
            return [];
        }
        return modelManager.GetMissingModelNames(ClientManager).ToList();
    }

    protected override Dictionary<string, ComfyNode> BuildWorkflow(ImageGenerationRequest request)
    {
        var (width, height) = GetDimensions(request);
        var (steps, cfg) = GetStepsAndCfg(request);

        return QwenImageWorkflowBuilder.Build(request, ClientManager, width, height, steps, cfg);
    }

    private (int? Steps, double? Cfg) GetStepsAndCfg(ImageGenerationRequest request)
    {
        int? steps = null;
        double? cfg = null;

        if (request.ProviderOptions != null)
        {
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
                Logger.LogInformation("Using Qwen Image overrides: Steps={Steps}, Cfg={Cfg}", steps, cfg);
            }
        }

        return (steps, cfg);
    }
}
