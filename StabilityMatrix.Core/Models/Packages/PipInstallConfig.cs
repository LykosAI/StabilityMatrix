namespace StabilityMatrix.Core.Models.Packages;

/// <summary>
/// Configuration for the standard pip installation process.
/// </summary>
public record PipInstallConfig
{
    public IEnumerable<string> RequirementsFilePaths { get; init; } = [];
    public string RequirementsExcludePattern { get; init; } = "(torch|torchvision|torchaudio|xformers)";
    public IEnumerable<string> PrePipInstallArgs { get; init; } = [];
    public IEnumerable<string> ExtraPipArgs { get; init; } = [];
    public IEnumerable<string> PostInstallPipArgs { get; init; } = [];
    public string TorchVersion { get; init; } = "";
    public string TorchvisionVersion { get; init; } = "";
    public string TorchaudioVersion { get; init; } = "";
    public string XformersVersion { get; init; } = "";
    public string CudaIndex { get; init; } = "cu130";
    public string RocmIndex { get; init; } = "rocm6.4";
    public bool ForceReinstallTorch { get; init; } = true;
    public bool UpgradePackages { get; init; } = false;
    public bool SkipTorchInstall { get; init; } = false;
}
