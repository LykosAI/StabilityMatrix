namespace StabilityMatrix.Core.Models.Api.Lykos;

public record GetFilesDownloadResponse
{
    public required Uri DownloadUrl { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }
}
