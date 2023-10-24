using Semver;
using StabilityMatrix.Core.Processes;

namespace StabilityMatrix.Core.Python;

public record PipInstallArgs : ProcessArgsBuilder
{
    public PipInstallArgs(params Argument[] arguments)
        : base(arguments) { }

    public PipInstallArgs WithTorch(string version = "") => this.AddArg($"torch{version}");

    public PipInstallArgs WithTorchDirectML(string version = "") =>
        this.AddArg($"torch-directml{version}");

    public PipInstallArgs WithTorchVision(string version = "") =>
        this.AddArg($"torchvision{version}");

    public PipInstallArgs WithXFormers(string version = "") => this.AddArg($"xformers{version}");

    public PipInstallArgs WithExtraIndex(string indexUrl) =>
        this.AddArg(("--extra-index-url", indexUrl));

    public PipInstallArgs WithTorchExtraIndex(string index) =>
        this.AddArg(("--extra-index-url", $"https://download.pytorch.org/whl/{index}"));

    public static PipInstallArgs GetTorch(string version = "") =>
        new() { Arguments = { $"torch{version}", "torchvision" } };

    public static PipInstallArgs GetTorchDirectML(string version = "") =>
        new() { Arguments = { $"torch-directml{version}" } };

    /// <inheritdoc />
    public override string ToString()
    {
        return base.ToString();
    }
}
