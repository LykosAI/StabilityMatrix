using StabilityMatrix.Core.Models.Api.CivitTRPC;
using StabilityMatrix.Core.Models.Api.Lykos;

namespace StabilityMatrix.Core.Models;

public readonly record struct Secrets
{
    public LykosAccountTokens? LykosAccount { get; init; }

    public CivitApiTokens? CivitApi { get; init; }
}
