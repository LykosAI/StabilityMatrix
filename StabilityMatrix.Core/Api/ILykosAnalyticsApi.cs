using System.ComponentModel;
using Refit;
using StabilityMatrix.Core.Models.Api.Lykos;
using StabilityMatrix.Core.Models.Api.Lykos.Analytics;

namespace StabilityMatrix.Core.Api;

[Localizable(false)]
[Headers("User-Agent: StabilityMatrix")]
public interface ILykosAnalyticsApi
{
    [Post("/api/analytics")]
    Task PostInstallData([Body] AnalyticsRequest data, CancellationToken cancellationToken = default);
}
