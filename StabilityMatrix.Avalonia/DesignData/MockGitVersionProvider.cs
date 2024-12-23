using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using StabilityMatrix.Core.Git;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Avalonia.DesignData;

public class MockGitVersionProvider : IGitVersionProvider
{
    public Task<IReadOnlyList<GitVersion>> FetchTagsAsync(
        int limit = 0,
        CancellationToken cancellationToken = default
    )
    {
        return Task.FromResult<IReadOnlyList<GitVersion>>(
            [
                new GitVersion { Tag = "v1.0.0" },
                new GitVersion { Tag = "v1.0.1" },
                new GitVersion { Tag = "v1.0.2" },
                new GitVersion { Tag = "v1.0.3" }
            ]
        );
    }

    public Task<IReadOnlyList<GitVersion>> FetchBranchesAsync(
        int limit = 0,
        CancellationToken cancellationToken = default
    )
    {
        return Task.FromResult<IReadOnlyList<GitVersion>>(
            [
                new GitVersion { Branch = "main" },
                new GitVersion { Branch = "develop" },
                new GitVersion { Branch = "feature/1" },
                new GitVersion { Branch = "feature/2" }
            ]
        );
    }

    public Task<IReadOnlyList<GitVersion>> FetchCommitsAsync(
        string? branch = null,
        int limit = 0,
        CancellationToken cancellationToken = default
    )
    {
        branch ??= "main";

        if (limit <= 0)
        {
            limit = 100;
        }

        // Generate sha1 hashes using branch as rng seed
        var rng = new Random(branch.GetHashCode());
        var hashes = Enumerable
            .Range(0, limit)
            .Select(_ =>
            {
                var data = new byte[32];
                rng.NextBytes(data);
                var hash = SHA1.HashData(data);
                return Convert.ToHexString(hash).ToLowerInvariant();
            })
            .ToArray();

        var results = hashes.Select(hash => new GitVersion { Branch = branch, CommitSha = hash }).ToArray();

        return Task.FromResult<IReadOnlyList<GitVersion>>(results);
    }
}
