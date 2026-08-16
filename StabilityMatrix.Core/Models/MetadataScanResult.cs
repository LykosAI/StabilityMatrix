namespace StabilityMatrix.Core.Models;

/// <summary>
/// Outcome tally of a bulk metadata scan, used to give the user an honest summary
/// instead of an unconditional success message.
/// </summary>
public sealed record MetadataScanResult
{
    /// <summary>Number of files considered by the scan.</summary>
    public required int Total { get; init; }

    /// <summary>Files whose metadata was found and written.</summary>
    public int Found { get; init; }

    /// <summary>Files whose hash is not known to CivitAI.</summary>
    public int NotFound { get; init; }

    /// <summary>Files that failed to process (hashing errors, I/O errors, API failures).</summary>
    public int Failed { get; init; }

    /// <summary>Files skipped because no model file was available to hash.</summary>
    public int Skipped { get; init; }

    /// <summary>
    /// True when the scan stopped early because CivitAI kept refusing requests
    /// (rate limiting or connectivity loss). Remaining files were not attempted.
    /// </summary>
    public bool Aborted { get; init; }
}
