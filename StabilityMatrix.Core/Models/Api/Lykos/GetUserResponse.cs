namespace StabilityMatrix.Core.Models.Api.Lykos;

public record GetUserResponse(
    string Id,
    LykosAccount Account,
    int UserLevel,
    string PatreonId,
    bool IsEmailVerified
);
