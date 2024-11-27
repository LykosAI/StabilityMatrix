using StabilityMatrix.Core.Models.Api.CivitTRPC;
using StabilityMatrix.Core.Models.Api.Lykos;

namespace StabilityMatrix.Core.Models;

public readonly record struct Secrets
{
    [Obsolete("Use LykosAccountV2 instead")]
    public LykosAccountV1Tokens? LykosAccount { get; init; }

    public CivitApiTokens? CivitApi { get; init; }

    public LykosAccountV2Tokens? LykosAccountV2 { get; init; }
}
