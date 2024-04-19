namespace StabilityMatrix.Core.Models.Api.Lykos;

public record GetUserResponse
{
    public required string Id { get; init; }
    public required LykosAccount Account { get; init; }
    public required HashSet<LykosRole> UserRoles { get; init; }
    public string? PatreonId { get; init; }
    public bool IsEmailVerified { get; init; }
    public bool CanHasDevBuild { get; init; }
    public bool CanHasPreviewBuild { get; init; }

    public bool IsActiveSupporter => CanHasDevBuild || CanHasPreviewBuild;
}
