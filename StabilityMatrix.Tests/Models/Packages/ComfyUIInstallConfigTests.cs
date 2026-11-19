using NSubstitute;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;
using StabilityMatrix.Core.Services.Rocm;

namespace StabilityMatrix.Tests.Models.Packages;

[TestClass]
public class ComfyUIInstallConfigTests
{
    private readonly ComfyUI comfy = new(
        Substitute.For<IGithubApiCache>(),
        Substitute.For<ISettingsManager>(),
        Substitute.For<IDownloadService>(),
        Substitute.For<IPrerequisiteHelper>(),
        Substitute.For<IPyInstallationManager>(),
        Substitute.For<IPipWheelService>(),
        Substitute.For<IRocmPackageHelper>()
    );

    [TestMethod]
    [DataRow("3.10.21", true)]
    [DataRow("3.11.14", true)]
    [DataRow("3.12.10", true)]
    [DataRow("3.12.99", true)]
    [DataRow("3.13.0", false)]
    [DataRow("3.13.12", false)]
    [DataRow("3.14.0", false)]
    public void Requirements_OnlyConstrainNumpyOnInterpretersSupportedByNumpy1(
        string pythonVersion,
        bool expectLegacyConstraint
    )
    {
        var config = comfy.GetStandardInstallConfig(PyVersion.Parse(pythonVersion), false);
        var args = new PipInstallArgs([.. config.ExtraPipArgs])
            .WithParsedFromRequirementsTxt(
                "numpy>=1.25.0\nscipy\ntorch\ntorchvision",
                config.RequirementsExcludePattern
            )
            .ToProcessArgs();

        Assert.AreEqual(expectLegacyConstraint, args.Contains("numpy<2"));
        Assert.IsTrue(args.Contains("numpy>=1.25.0"));
        Assert.IsTrue(args.Contains("scipy"));
        Assert.IsFalse(args.Contains("torch"));
        Assert.IsFalse(args.Contains("torchvision"));
    }

    [TestMethod]
    [DataRow(true, "cu126")]
    [DataRow(false, "cu130")]
    public void Python313_PreservesTorchAndRequirementsConfiguration(bool isLegacyNvidia, string cudaIndex)
    {
        var config = comfy.GetStandardInstallConfig(new PyVersion(3, 13, 0), isLegacyNvidia);

        CollectionAssert.AreEqual(new[] { "requirements.txt" }, config.RequirementsFilePaths.ToArray());
        Assert.AreEqual(cudaIndex, config.CudaIndex);
        Assert.AreEqual("rocm7.14", config.RocmIndex);
        Assert.AreEqual("xpu", config.XpuIndex);
        Assert.AreEqual(" ", config.TorchaudioVersion);
        Assert.IsTrue(config.UpgradePackages);
        Assert.IsTrue(config.ForceReinstallTorch);
        CollectionAssert.AreEqual(new[] { "typing-extensions>=4.15.0" }, config.PostInstallPipArgs.ToArray());
    }
}
