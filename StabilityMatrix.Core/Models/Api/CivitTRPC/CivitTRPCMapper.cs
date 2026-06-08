namespace StabilityMatrix.Core.Models.Api.CivitTRPC;

/// <summary>
/// Maps a tRPC <c>model.getById</c> response into the public-REST-API
/// <see cref="CivitModel"/> shape that the rest of the app expects.
/// Used as a fallback when the public REST endpoint returns an empty
/// <c>modelVersions</c> list due to CivitAI's server-side cache desync.
/// </summary>
public static class CivitTRPCMapper
{
    /// <summary>
    /// Returns a list of <see cref="CivitModelVersion"/> built from a tRPC model response.
    /// Intended to be grafted onto an existing <see cref="CivitModel"/> whose
    /// own modelVersions came back empty.
    /// </summary>
    public static List<CivitModelVersion> ToModelVersions(CivitTRPCModel trpcModel)
    {
        if (trpcModel.ModelVersions is null || trpcModel.ModelVersions.Count == 0)
            return [];

        return trpcModel
            .ModelVersions.Where(v => string.IsNullOrEmpty(v.Status) || v.Status == "Published")
            .Select(ToModelVersion)
            .ToList();
    }

    private static CivitModelVersion ToModelVersion(CivitTRPCModelVersion v)
    {
        return new CivitModelVersion
        {
            Id = v.Id,
            Name = v.Name ?? string.Empty,
            Description = v.Description ?? string.Empty,
            BaseModel = v.BaseModel,
            Availability = v.Availability,
            PublishedAt = v.PublishedAt,
            TrainedWords = v.TrainedWords ?? [],
            // The REST API exposes a constructed download URL we don't have on tRPC; leaving
            // it null. Per-file DownloadUrl is what download flows actually consume.
            DownloadUrl = string.Empty,
            // Stats aren't in the tRPC payload in a comparable shape; leave default.
            Stats = new CivitModelStats(),
            Files = v.Files?.Select(ToFile).ToList(),
            Images = [],
        };
    }

    private static CivitFile ToFile(CivitTRPCFile f)
    {
        return new CivitFile
        {
            Id = f.Id,
            Name = f.Name ?? string.Empty,
            // tRPC exposes the raw storage URL as `url`; the REST API exposes a
            // download-redirect URL as `downloadUrl`. The raw URL works for the
            // download service path the same way the redirect URL does.
            DownloadUrl = f.Url ?? string.Empty,
            SizeKb = f.SizeKb,
            Type = f.Type,
            Metadata = f.Metadata ?? new CivitFileMetadata(),
            PickleScanResult = f.PickleScanResult ?? string.Empty,
            VirusScanResult = f.VirusScanResult ?? string.Empty,
            ScannedAt = f.ScannedAt,
            Hashes = ToFileHashes(f.Hashes),
            // tRPC doesn't include a `primary` flag — the REST API has it pre-computed.
            // Downstream code uses getPrimaryFile-style heuristics anyway, so default false.
            IsPrimary = false,
        };
    }

    private static CivitFileHashes ToFileHashes(List<CivitTRPCFileHash>? hashes)
    {
        var result = new CivitFileHashes();
        if (hashes is null)
            return result;

        foreach (var entry in hashes)
        {
            if (string.IsNullOrEmpty(entry.Type) || string.IsNullOrEmpty(entry.Hash))
                continue;

            // Match the keys exposed by the REST hashes object. CivitFileHashes only stores
            // SHA256/CRC32/BLAKE3 — other types (AutoV1/V2/V3) are dropped to match REST behavior.
            switch (entry.Type.ToUpperInvariant())
            {
                case "SHA256":
                    result.SHA256 = entry.Hash;
                    break;
                case "CRC32":
                    result.CRC32 = entry.Hash;
                    break;
                case "BLAKE3":
                    result.BLAKE3 = entry.Hash;
                    break;
            }
        }

        return result;
    }
}
