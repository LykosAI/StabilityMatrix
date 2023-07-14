namespace StabilityMatrix.Core.Models.Progress;

public record struct ProgressItem(Guid ProgressId, string Name, ProgressReport Progress, bool Failed = false);
