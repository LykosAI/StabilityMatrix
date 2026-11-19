using Microsoft.Extensions.Logging;
using StabilityMatrix.Avalonia.Models.BananaVision;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;
using StabilityMatrix.Core.Services.ImageGeneration;

namespace StabilityMatrix.Avalonia.Services;

/// <summary>
/// Image Lab provider for Krea 2 via the local ComfyUI backend. Text-to-image only: the
/// workflow has no reference-image path, so image input is not supported.
/// </summary>
public class Krea2Provider(
    ILogger<Krea2Provider> logger,
    IInferenceClientManager clientManager,
    RunningPackageService runningPackageService,
    INotificationService notificationService
) : ComfyImageGenerationProviderBase(logger, clientManager, runningPackageService, notificationService)
{
    public override string ProviderId => BananaVisionProviderIds.Krea2;
    public override string ProviderName => "Krea 2 (Local)";

    public override bool SupportsImageInput => false;

    protected override string LogName => "Krea 2";
    protected override int MaxInputImages => 0;
    protected override string ProviderPrefix => "krea2";

    protected override IReadOnlyList<string> GetMissingModels(ImageGenerationRequest request) =>
        new Krea2ModelManager().GetMissingModelNames(ClientManager).ToList();

    protected override Dictionary<string, ComfyNode> BuildWorkflow(ImageGenerationRequest request)
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
        }

        var (width, height) = GetDimensions(request);

        return Krea2WorkflowBuilder.Build(
            request,
            ClientManager,
            GetCustomUnetModel(request),
            GetSelectedLoras(request),
            width,
            height,
            steps,
            cfg
        );
    }
}
