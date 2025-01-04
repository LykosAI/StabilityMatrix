using System.ComponentModel;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Processes;

namespace StabilityMatrix.Core.Git;

/// <summary>
/// Fetch git versions via commands to a git process runner
/// </summary>
[Localizable(false)]
public class CommandGitVersionProvider(string repositoryUri, IPrerequisiteHelper prerequisiteHelper)
    : IGitVersionProvider
{
    public async Task<IReadOnlyList<GitVersion>> FetchTagsAsync(
        int limit = 0,
        CancellationToken cancellationToken = default
    )
    {
        var tags = new List<GitVersion>();
        ProcessArgs args =
        [
            "-c",
            "versionsort.suffix=-",
            "ls-remote",
            "--tags",
            "--sort=-v:refname",
            repositoryUri
        ];

        var result = await prerequisiteHelper
            .GetGitOutput(args)
            .EnsureSuccessExitCode()
            .ConfigureAwait(false);

        if (result is { IsSuccessExitCode: true, StandardOutput: not null })
        {
            var tagLines = result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var tagNames = tagLines
                .Select(line => line.Split('\t').LastOrDefault()?.Replace("refs/tags/", "").Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Take(limit > 0 ? limit : int.MaxValue);

            tags.AddRange(tagNames.Select(tag => new GitVersion { Tag = tag }));
        }

        return tags;
    }

    public async Task<IReadOnlyList<GitVersion>> FetchBranchesAsync(
        int limit = 0,
        CancellationToken cancellationToken = default
    )
    {
        var branches = new List<GitVersion>();
        ProcessArgs args = ["ls-remote", "--heads", repositoryUri];

        var result = await prerequisiteHelper
            .GetGitOutput(args)
            .EnsureSuccessExitCode()
            .ConfigureAwait(false);

        if (result is { IsSuccessExitCode: true, StandardOutput: not null })
        {
            var branchLines = result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var branchNames = branchLines
                .Select(line => line.Split('\t').LastOrDefault()?.Replace("refs/heads/", "").Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Take(limit > 0 ? limit : int.MaxValue);

            branches.AddRange(branchNames.Select(branch => new GitVersion { Branch = branch }));
        }

        return branches;
    }

    public async Task<IReadOnlyList<GitVersion>> FetchCommitsAsync(
        string? branch = null,
        int limit = 0,
        CancellationToken cancellationToken = default
    )
    {
        // Cannot use ls-remote, so clone to temp directory and fetch
        var commits = new List<GitVersion>();

        using var tempDirectory = new TempDirectoryPath();

        ProcessArgs args = ["clone", "--bare", "--filter=tree:0", "--single-branch"];

        if (!string.IsNullOrEmpty(branch))
        {
            args = args.Concat(["--branch", branch]);
        }

        args = args.Concat([repositoryUri, tempDirectory.FullPath]);

        _ = await prerequisiteHelper.GetGitOutput(args).EnsureSuccessExitCode().ConfigureAwait(false);

        _ = await prerequisiteHelper
            .GetGitOutput(["fetch", "--all"], tempDirectory.FullPath)
            .EnsureSuccessExitCode()
            .ConfigureAwait(false);

        // If not branch not specified, get it now
        if (string.IsNullOrEmpty(branch))
        {
            var branchResult = await prerequisiteHelper
                .GetGitOutput(["rev-parse", "--abbrev-ref", "HEAD"], tempDirectory.FullPath)
                .EnsureSuccessExitCode()
                .ConfigureAwait(false);

            if (string.IsNullOrEmpty(branchResult.StandardOutput?.Trim()))
            {
                // Could not get branch
                return [];
            }

            branch = branchResult.StandardOutput.Trim();
        }

        ProcessArgs logArgs = ["log", "--pretty=format:%H", "--no-decorate"];

        if (limit > 0)
        {
            logArgs = logArgs.Concat([$"--max-count={limit}"]);
        }

        var logResult = await prerequisiteHelper
            .GetGitOutput(logArgs.Concat([branch]), tempDirectory.FullPath)
            .EnsureSuccessExitCode()
            .ConfigureAwait(false);

        if (logResult is { StandardOutput: not null })
        {
            var commitLines = logResult.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            // Only accept lines of 40 characters and valid hexadecimal hash
            var commitHashes = commitLines
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Where(line => line.Length == 40 && line.All(char.IsLetterOrDigit));

            var commitObjs = commitHashes
                .Take(limit > 0 ? limit : int.MaxValue)
                .Select(commitHash => new GitVersion { Branch = branch, CommitSha = commitHash });

            commits.AddRange(commitObjs);
        }

        return commits;
    }
}
