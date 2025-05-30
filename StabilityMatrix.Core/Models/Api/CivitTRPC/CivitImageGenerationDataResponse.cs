using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api.CivitTRPC;

public class CivitImageGenerationDataResponse
{
    [JsonPropertyName("process")]
    public string? Process { get; set; }

    [JsonPropertyName("meta")]
    public CivitImageMetadata? Metadata { get; set; }

    [JsonPropertyName("resources")]
    public List<CivitImageResource>? Resources { get; set; }

    [JsonIgnore]
    public IReadOnlyDictionary<string, string>? OtherMetadata { get; set; }
}

public class CivitImageMetadata
{
    [JsonPropertyName("prompt")]
    public string? Prompt { get; set; }

    [JsonPropertyName("negativePrompt")]
    public string? NegativePrompt { get; set; }

    [JsonPropertyName("cfgScale")]
    public double? CfgScale { get; set; }

    [JsonPropertyName("steps")]
    public int? Steps { get; set; }

    [JsonPropertyName("sampler")]
    public string? Sampler { get; set; }

    [JsonPropertyName("seed")]
    public long? Seed { get; set; }

    [JsonPropertyName("Eta")]
    public string? Eta { get; set; }

    [JsonPropertyName("RNG")]
    public string? Rng { get; set; }

    [JsonPropertyName("ENSD")]
    public string? Ensd { get; set; }

    [JsonPropertyName("Size")]
    public string? Size { get; set; }

    [JsonPropertyName("width")]
    public int? Width { get; set; }

    [JsonPropertyName("height")]
    public int? Height { get; set; }

    [JsonPropertyName("Model")]
    public string? Model { get; set; }

    [JsonPropertyName("Version")]
    public string? Version { get; set; }

    [JsonPropertyName("resources")]
    public List<CivitImageResource>? Resources { get; set; }

    [JsonPropertyName("ModelHash")]
    public string? ModelHash { get; set; }

    [JsonPropertyName("Hires steps")]
    public string? HiresSteps { get; set; }

    [JsonPropertyName("Hires upscale")]
    public string? HiresUpscaleAmount { get; set; }

    [JsonPropertyName("Schedule type")]
    public string? ScheduleType { get; set; }

    [JsonPropertyName("Hires upscaler")]
    public string? HiresUpscaler { get; set; }

    [JsonPropertyName("Denoising strength")]
    public string? DenoisingStrength { get; set; }

    [JsonPropertyName("clipSkip")]
    public int? ClipSkip { get; set; }

    [JsonPropertyName("scheduler")]
    public string? Scheduler { get; set; }

    [JsonIgnore]
    public string Dimensions => string.IsNullOrWhiteSpace(Size) ? $"{Width}x{Height}" : Size;
}

public class CivitImageResource
{
    public string? Hash { get; set; }
    public string? Name { get; set; }
    public string? Type { get; set; }
    public int ModelId { get; set; }
    public string? ModelType { get; set; }
    public string? ModelName { get; set; }
    public int VersionId { get; set; }
    public string? VersionName { get; set; }
}
