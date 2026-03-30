using System.IO;
using StabilityMatrix.Core.Models.Database;

namespace StabilityMatrix.Avalonia.Models.CheckpointOrganizer;

public enum ModelOrganizationPreviewStatus
{
    Ready,
    Conflict,
    Skipped,
    Unchanged,
}

public enum ModelOrganizationMetadataAction
{
    None,
    ScanMissing,
    UpdateExisting,
}

public sealed record ModelOrganizationFileMove
{
    public required string SourcePath { get; init; }
    public required string TargetPath { get; init; }
}

public sealed record ModelOrganizationPreviewItem
{
    public required LocalModelFile Model { get; init; }
    public required string SourcePath { get; init; }
    public string? TargetPath { get; init; }
    public required ModelOrganizationPreviewStatus Status { get; init; }
    public string? Reason { get; init; }
    public IReadOnlyList<ModelOrganizationFileMove> FileMoves { get; init; } = [];

    public bool CanApply => Status == ModelOrganizationPreviewStatus.Ready;

    public bool IsUnchanged => Status == ModelOrganizationPreviewStatus.Unchanged;

    public string SourceFileName => Path.GetFileName(SourcePath);
    public string? TargetFileName => TargetPath is not null ? Path.GetFileName(TargetPath) : null;

    public int SortOrder =>
        Status switch
        {
            ModelOrganizationPreviewStatus.Ready => 0,
            ModelOrganizationPreviewStatus.Conflict => 1,
            ModelOrganizationPreviewStatus.Skipped => 2,
            ModelOrganizationPreviewStatus.Unchanged => 3,
            _ => 4,
        };

    public string StatusText =>
        Status switch
        {
            ModelOrganizationPreviewStatus.Ready => "Ready",
            ModelOrganizationPreviewStatus.Conflict => "Conflict",
            ModelOrganizationPreviewStatus.Skipped => "Skipped",
            ModelOrganizationPreviewStatus.Unchanged => "Unchanged",
            _ => Status.ToString(),
        };
}

public sealed record ModelOrganizationPlan
{
    public required string Template { get; init; }
    public required string ScopePath { get; init; }
    public required bool IncludeNested { get; init; }
    public string? ValidationError { get; init; }
    public IReadOnlyList<ModelOrganizationPreviewItem> Items { get; init; } = [];

    public int ReadyCount => Items.Count(item => item.Status == ModelOrganizationPreviewStatus.Ready);

    public int ConflictCount => Items.Count(item => item.Status == ModelOrganizationPreviewStatus.Conflict);

    public int SkippedCount =>
        Items.Count(item =>
            item.Status is ModelOrganizationPreviewStatus.Skipped or ModelOrganizationPreviewStatus.Unchanged
        );
}

public sealed record ModelOrganizationApplyResult
{
    public required int MovedCount { get; init; }
    public required int SkippedCount { get; init; }
    public required int ConflictCount { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
}
