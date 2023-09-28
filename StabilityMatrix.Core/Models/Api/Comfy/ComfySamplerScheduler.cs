namespace StabilityMatrix.Core.Models.Api.Comfy;

/// <summary>
/// Pair of <see cref="ComfySampler"/> and <see cref="ComfyScheduler"/>
/// </summary>
public readonly record struct ComfySamplerScheduler(ComfySampler Sampler, ComfyScheduler Scheduler)
{
    /// <inheritdoc />
    public bool Equals(ComfySamplerScheduler other)
    {
        return Sampler.Equals(other.Sampler) && Scheduler.Equals(other.Scheduler);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(Sampler, Scheduler);
    }

    // Implicit conversion from (ComfySampler, ComfyScheduler)
    public static implicit operator ComfySamplerScheduler((ComfySampler, ComfyScheduler) tuple)
    {
        return new ComfySamplerScheduler(tuple.Item1, tuple.Item2);
    }
}
