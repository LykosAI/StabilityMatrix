namespace StabilityMatrix.Core.Helper;

/// <summary>
/// Verifies whether a file on disk matches an expected content SHA256, caching each path's computed
/// hash so a path shared by several candidates (e.g. multiple repo files that infer the same
/// destination) is hashed at most once. Use this to tell apart same-named-but-different-content
/// files that the model index can't distinguish (it only knows hashes from <c>.cm-info.json</c>
/// sidecars).
/// </summary>
/// <remarks>
/// Not thread-safe: intended to be driven from a single thread (e.g. a UI-thread verification loop).
/// Hashing itself runs on the thread pool, but results are read/written from the calling thread.
/// </remarks>
public sealed class OnDiskHashVerifier
{
    // Computed SHA256 per absolute path; null means no file was present at that path.
    private readonly Dictionary<string, string?> hashByPath = new();

    /// <summary>
    /// Returns true if a file exists at <paramref name="path"/> and its SHA256 equals
    /// <paramref name="expectedSha256"/> (case-insensitive). Returns false if the file is absent,
    /// unreadable, or the operation is cancelled mid-hash.
    /// </summary>
    public async Task<bool> MatchesAsync(string path, string expectedSha256, CancellationToken ct = default)
    {
        if (!hashByPath.TryGetValue(path, out var actual))
        {
            try
            {
                actual = File.Exists(path)
                    ? await Task.Run(() => FileHash.GetSha256Async(path), ct).ConfigureAwait(false)
                    : null;
            }
            catch (Exception)
            {
                // Locked / removed / permission issue: treat as unknown so the caller keeps going.
                actual = null;
            }

            // The hash ran on a background thread; if we were cancelled meanwhile, don't cache a
            // partial/aborted result (the cache may have been cleared for a new context).
            if (ct.IsCancellationRequested)
                return false;

            hashByPath[path] = actual;
        }

        return actual is not null
            && string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Forget all cached hashes (e.g. when switching to a new set of candidates).</summary>
    public void Clear() => hashByPath.Clear();
}
