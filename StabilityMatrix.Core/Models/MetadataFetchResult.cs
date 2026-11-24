namespace StabilityMatrix.Core.Models;

public enum MetadataFetchStatus
{
    Found,

    /// <summary>Metadata already exists and re-import was not requested; nothing was fetched.</summary>
    AlreadyExists,

    /// <summary>No BLAKE3 hash could be obtained for the file.</summary>
    HashUnavailable,

    /// <summary>The file's hash is not known to CivitAI.</summary>
    NotFound,

    /// <summary>CivitAI is rate limiting requests; trying again later may succeed.</summary>
    RateLimited,

    /// <summary>CivitAI could not be reached or returned an unexpected error.</summary>
    Unavailable,
}

/// <summary>
/// Outcome of a single-file metadata lookup. <see cref="Metadata"/> is non-null
/// only when <see cref="Status"/> is <see cref="MetadataFetchStatus.Found"/>.
/// </summary>
public sealed record MetadataFetchResult(MetadataFetchStatus Status, ConnectedModelInfo? Metadata);
