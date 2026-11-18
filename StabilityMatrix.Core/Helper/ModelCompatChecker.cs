using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api;

namespace StabilityMatrix.Core.Helper;

public class ModelCompatChecker
{
    private readonly Dictionary<string, CivitBaseModelType> baseModelNamesToTypes =
        Enum.GetValues<CivitBaseModelType>().ToDictionary(x => x.GetStringValue());

    public bool? IsLoraCompatibleWithBaseModel(HybridModelFile? lora, HybridModelFile? baseModel)
    {
        // Require a known base model for both
        if (
            lora?.Local?.DisplayBaseModel is not { } loraBaseModel
            || !baseModelNamesToTypes.TryGetValue(loraBaseModel, out var loraBaseModelType)
        )
            return null;

        if (
            baseModel?.Local?.DisplayBaseModel is not { } baseModelName
            || !baseModelNamesToTypes.TryGetValue(baseModelName, out var baseModelType)
        )
            return null;

        // Normalize both
        var normalizedLoraBaseModelType = NormalizeBaseModelType(loraBaseModelType);
        var normalizedBaseModelType = NormalizeBaseModelType(baseModelType);

        // Ignore if either is "Other"
        if (
            normalizedLoraBaseModelType == CivitBaseModelType.Other
            || normalizedBaseModelType == CivitBaseModelType.Other
        )
            return null;

        return normalizedLoraBaseModelType == normalizedBaseModelType;
    }

    // Normalize base model type
    private static CivitBaseModelType NormalizeBaseModelType(CivitBaseModelType baseModel)
    {
        return baseModel switch
        {
            CivitBaseModelType.Sdxl09 => CivitBaseModelType.Sdxl10,
            CivitBaseModelType.Sdxl10Lcm => CivitBaseModelType.Sdxl10,
            CivitBaseModelType.SdxlDistilled => CivitBaseModelType.Sdxl10,
            CivitBaseModelType.SdxlHyper => CivitBaseModelType.Sdxl10,
            CivitBaseModelType.SdxlLightning => CivitBaseModelType.Sdxl10,
            CivitBaseModelType.SdxlTurbo => CivitBaseModelType.Sdxl10,
            CivitBaseModelType.Pony => CivitBaseModelType.Sdxl10,
            CivitBaseModelType.NoobAi => CivitBaseModelType.Sdxl10,
            CivitBaseModelType.Illustrious => CivitBaseModelType.Sdxl10,
            _ => baseModel,
        };
    }
}
