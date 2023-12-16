namespace StabilityMatrix.Core.Models.Api.Lykos;

public record GetUserResponse
{
    public required string Id { get; init; }
    public required LykosAccount Account { get; init; }
    public required HashSet<LykosRole> UserRoles { get; init; }
    public string? PatreonId { get; init; }
    public bool IsEmailVerified { get; init; }

    public bool IsActiveSupporter =>
        UserRoles.Contains(LykosRole.PatreonSupporter)
        || UserRoles.Contains(LykosRole.Insider)
        || (UserRoles.Contains(LykosRole.Developer) && PatreonId is not null);
}
