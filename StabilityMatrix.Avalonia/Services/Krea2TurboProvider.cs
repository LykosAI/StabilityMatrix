using Microsoft.Extensions.Logging;
using StabilityMatrix.Avalonia.Models.BananaVision;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;
using StabilityMatrix.Core.Services.ImageGeneration;

namespace StabilityMatrix.Avalonia.Services;

/// <summary>
/// Image generation provider for Krea 2 Turbo using local ComfyUI backend.
/// Text-to-image only; the 8-step distilled checkpoint keeps generations fast enough for
/// the conversational surface. Krea 2 is under the Krea AI Community License.
/// </summary>
public class Krea2TurboProvider(
    ILogger<Krea2TurboProvider> logger,
    IInferenceClientManager clientManager,
    RunningPackageService runningPackageService,
    INotificationService notificationService
) : ComfyImageGenerationProviderBase(logger, clientManager, runningPackageService, notificationService)
{
    public override string ProviderId => BananaVisionProviderIds.Krea2Turbo;
    public override string ProviderName => "Krea 2 Turbo (Local)";

    public override bool SupportsImageInput => false;

    protected override string LogName => "Krea 2 Turbo";
    protected override int MaxInputImages => 0;
    protected override string ProviderPrefix => "krea2";

    protected override IReadOnlyList<string> GetMissingModels(ImageGenerationRequest request)
    {
        var modelManager = new Krea2TurboModelManager();
        if (modelManager.AreModelsAvailable(ClientManager))
        {
            return [];
        }
        return modelManager.GetMissingModelNames(ClientManager).ToList();
    }

    protected override Dictionary<string, ComfyNode> BuildWorkflow(ImageGenerationRequest request)
    {
        var (width, height) = GetDimensions(request);

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
                Logger.LogInformation("Using Krea 2 overrides: Steps={Steps}, Cfg={Cfg}", steps, cfg);
            }
        }

        return Krea2TurboWorkflowBuilder.Build(request, ClientManager, width, height, steps, cfg);
    }
}
