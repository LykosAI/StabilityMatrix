namespace StabilityMatrix.Core.Models.Api.CivArchive;

/// <summary>
/// Result of resolving a <c>/sha256/{hash}</c> file URL: the canonical model-version page
/// for the file, plus a gallery image usable as a card thumbnail when available.
/// </summary>
public sealed record CivArchiveFileResolution(string ModelUrl, string? ImageUrl);
