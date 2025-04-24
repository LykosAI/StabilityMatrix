using System.Collections.Immutable;

namespace StabilityMatrix.Core.Models.Api.Comfy;

public readonly record struct ComfyScheduler(string Name)
{
    public static ComfyScheduler Normal { get; } = new("normal");
    public static ComfyScheduler Karras { get; } = new("karras");
    public static ComfyScheduler Exponential { get; } = new("exponential");
    public static ComfyScheduler SDTurbo { get; } = new("sd_turbo");
    public static ComfyScheduler Simple { get; } = new("simple");
    public static ComfyScheduler Beta { get; } = new("beta");
    public static ComfyScheduler AlignYourSteps { get; } = new("align_your_steps");
    public static ComfyScheduler LinearQuadratic { get; } = new("linear_quadratic");
    public static ComfyScheduler KLOptimal { get; } = new("kl_optimal");
    public static ComfyScheduler FaceDetailerAlignYourStepsSD1 { get; } = new("AYS SD1");
    public static ComfyScheduler FaceDetailerAlignYourStepsSDXL { get; } = new("AYS SDXL");
    public static ComfyScheduler FaceDetailerGits { get; } = new("GITS[coeff=1.2]");
    public static ComfyScheduler FaceDetailerLtxv { get; } = new("LTXV[default]");

    private static Dictionary<string, string> ConvertDict { get; } =
        new()
        {
            [Normal.Name] = "Normal",
            [Karras.Name] = "Karras",
            [Exponential.Name] = "Exponential",
            ["sgm_uniform"] = "SGM Uniform",
            [Simple.Name] = "Simple",
            ["ddim_uniform"] = "DDIM Uniform",
            [SDTurbo.Name] = "SD Turbo",
            [Beta.Name] = "Beta",
            [AlignYourSteps.Name] = "Align Your Steps",
            [LinearQuadratic.Name] = "Linear Quadratic",
            [KLOptimal.Name] = "KL Optimal"
        };

    private static Dictionary<string, string> FaceDetailerConvertDict { get; } =
        new()
        {
            [FaceDetailerAlignYourStepsSD1.Name] = "Align Your Steps SD1",
            [FaceDetailerAlignYourStepsSDXL.Name] = "Align Your Steps SDXL",
            [FaceDetailerGits.Name] = "GITS[coeff=1.2]",
            [FaceDetailerLtxv.Name] = "LTXV[default]"
        };

    public static IReadOnlyList<ComfyScheduler> Defaults { get; } =
        ConvertDict.Keys.Select(k => new ComfyScheduler(k)).ToImmutableArray();

    public static IReadOnlyList<ComfyScheduler> FaceDetailerDefaults { get; } =
        Defaults
            .Except([AlignYourSteps])
            .Concat(FaceDetailerConvertDict.Keys.Select(k => new ComfyScheduler(k)))
            .ToImmutableArray();

    public string DisplayName => ConvertDict.GetValueOrDefault(Name, Name);

    private sealed class NameEqualityComparer : IEqualityComparer<ComfyScheduler>
    {
        public bool Equals(ComfyScheduler x, ComfyScheduler y)
        {
            return x.Name == y.Name;
        }

        public int GetHashCode(ComfyScheduler obj)
        {
            return obj.Name.GetHashCode();
        }
    }

    public static IEqualityComparer<ComfyScheduler> Comparer { get; } = new NameEqualityComparer();
}
