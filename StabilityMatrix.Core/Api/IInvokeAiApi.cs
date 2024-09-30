using Refit;
using StabilityMatrix.Core.Models.Api.Invoke;

namespace StabilityMatrix.Core.Api;

[Headers("User-Agent: StabilityMatrix")]
public interface IInvokeAiApi
{
    [Get("/api/v2/models/scan_folder")]
    Task<List<ScanFolderResult>> ScanFolder(
        [Query, AliasAs("scan_path")] string scanPath,
        CancellationToken cancellationToken = default
    );

    [Post("/api/v2/models/install")]
    Task InstallModel(
        [Body] InstallModelRequest request,
        [Query] string source,
        [Query] bool inplace = true,
        CancellationToken cancellationToken = default
    );

    [Get("/api/v2/models/install")]
    Task<List<ModelInstallResult>> GetModelInstallStatus(CancellationToken cancellationToken = default);
}
