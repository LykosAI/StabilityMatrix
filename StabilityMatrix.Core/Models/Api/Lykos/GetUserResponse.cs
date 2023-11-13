namespace StabilityMatrix.Core.Models.Api.Lykos;

public record GetUserResponse(
    string Id,
    string AccountId,
    int UserLevel,
    string PatreonId,
    bool IsEmailVerified
);
