namespace StabilityMatrix.Core.Models.Settings;

/// <summary>
/// How much the hardware analytics category reveals about the machine.
/// </summary>
public enum HardwareDetailLevel
{
    /// <summary>GPU model name, exact memory sizes, full driver version.</summary>
    Detailed,

    /// <summary>
    /// GPU vendor only, memory rounded down in coarse steps, driver major version only. Compute
    /// capability is kept since it names an architecture generation, not a card.
    /// </summary>
    Minimal,
}
