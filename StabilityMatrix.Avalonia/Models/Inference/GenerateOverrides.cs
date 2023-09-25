namespace StabilityMatrix.Avalonia.Models.Inference;

public class GenerateOverrides
{
    public bool? IsHiresFixEnabled { get; set; }
    public bool? UseCurrentSeed { get; set; }

    public static GenerateOverrides FromFlags(GenerateFlags flags)
    {
        var overrides = new GenerateOverrides
        {
            IsHiresFixEnabled = flags.HasFlag(GenerateFlags.HiresFixEnable)
                ? true
                : flags.HasFlag(GenerateFlags.HiresFixDisable)
                    ? false
                    : null,
            UseCurrentSeed = flags.HasFlag(GenerateFlags.UseCurrentSeed)
                ? true
                : flags.HasFlag(GenerateFlags.UseRandomSeed)
                    ? false
                    : null
        };

        return overrides;
    }
}
