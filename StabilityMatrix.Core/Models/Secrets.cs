using StabilityMatrix.Core.Models.Api.CivitTRPC;
using StabilityMatrix.Core.Models.Api.Lykos;

namespace StabilityMatrix.Core.Models;

public readonly record struct Secrets
{
    [Obsolete("Use LykosAccountV2 instead")]
    public LykosAccountV1Tokens? LykosAccount { get; init; }

    public CivitApiTokens? CivitApi { get; init; }

    public LykosAccountV2Tokens? LykosAccountV2 { get; init; }

    public string? HuggingFaceToken { get; init; }
}

public static class SecretsExtensions
{
    public static bool HasLegacyLykosAccount(this Secrets secrets)
    {
#pragma warning disable CS0618 // Type or member is obsolete
        return secrets.LykosAccount is not null;
#pragma warning restore CS0618 // Type or member is obsolete
    }
}
