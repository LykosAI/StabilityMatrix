using Microsoft.Extensions.Logging;
using StabilityMatrix.Avalonia.Models.BananaVision;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;
using StabilityMatrix.Core.Services.ImageGeneration;

namespace StabilityMatrix.Avalonia.Services;

/// <summary>
/// Image generation provider for Qwen Image Edit using local ComfyUI backend
/// </summary>
public class QwenImageEditProvider(
    ILogger<QwenImageEditProvider> logger,
    IInferenceClientManager clientManager
) : ComfyImageGenerationProviderBase(logger, clientManager)
{
    public override string ProviderId => BananaVisionProviderIds.QwenImageEdit;
    public override string ProviderName => "Qwen Image Edit (Local)";

    protected override string LogName => "Qwen Image Edit";
    protected override int MaxInputImages => 3;
    protected override string ProviderPrefix => "qwen_image_edit";

    protected override IReadOnlyList<string> GetMissingModels(ImageGenerationRequest request)
    {
        var modelManager = new QwenImageEditModelManager();
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

        return QwenImageEditWorkflowBuilder.Build(
            request,
            ClientManager,
            customUnetModel,
            loras,
            width,
            height
        );
    }
}
