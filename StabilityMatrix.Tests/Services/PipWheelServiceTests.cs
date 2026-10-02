using NSubstitute;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Helper.HardwareInfo;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Tests.Services;

[TestClass]
public class PipWheelServiceTests
{
    private static readonly PyVersion Py310 = new(3, 10, 11);
    private static readonly PyVersion Py312 = new(3, 12, 7);

    // Real asset names from woct0rdho/SageAttention releases
    private const string Post6Cu128Torch210 =
        "sageattention-2.2.0+cu128torch2.10.0andhigher.post6-cp310-abi3-win_amd64.whl";
    private const string Post6Cu130Torch291 =
        "sageattention-2.2.0+cu130torch2.9.1.post6-cp310-abi3-win_amd64.whl";
    private const string Post3Cu126Torch260 =
        "sageattention-2.2.0+cu126torch2.6.0.post3-cp39-abi3-win_amd64.whl";
    private const string V211Cu128Torch270Cp310 =
        "sageattention-2.1.1+cu128torch2.7.0-cp310-cp310-win_amd64.whl";

    [TestMethod]
    public void AndHigherWheel_MatchesNewerTorch()
    {
        Assert.IsTrue(PipWheelService.IsMatchingSageWheel(Post6Cu128Torch210, "2.11.0", "cu128", Py312));
        Assert.IsTrue(PipWheelService.IsMatchingSageWheel(Post6Cu128Torch210, "2.10.0", "cu128", Py310));
    }

    [TestMethod]
    public void AndHigherWheel_RejectsOlderTorch()
    {
        Assert.IsFalse(PipWheelService.IsMatchingSageWheel(Post6Cu128Torch210, "2.9.1", "cu128", Py312));
    }

    [TestMethod]
    public void PinnedWheel_MatchesSameTorchMinorLine()
    {
        Assert.IsTrue(PipWheelService.IsMatchingSageWheel(Post6Cu130Torch291, "2.9.0", "cu130", Py312));
        Assert.IsTrue(PipWheelService.IsMatchingSageWheel(Post6Cu130Torch291, "2.9.1", "cu130", Py310));
    }

    [TestMethod]
    public void PinnedWheel_RejectsDifferentTorchMinor()
    {
        // The old prefix matching ("torch2.1" in "torch2.10...") must not resurface here
        Assert.IsFalse(PipWheelService.IsMatchingSageWheel(Post6Cu130Torch291, "2.10.0", "cu130", Py312));
        Assert.IsFalse(PipWheelService.IsMatchingSageWheel(Post3Cu126Torch260, "2.60.0", "cu126", Py312));
    }

    [TestMethod]
    public void Cp310Abi3Wheel_RequiresPython310OrNewer()
    {
        var py39 = new PyVersion(3, 9, 13);
        Assert.IsFalse(PipWheelService.IsMatchingSageWheel(Post6Cu128Torch210, "2.10.0", "cu128", py39));
        Assert.IsTrue(PipWheelService.IsMatchingSageWheel(Post3Cu126Torch260, "2.6.0", "cu126", py39));
    }

    [TestMethod]
    public void InterpreterSpecificWheel_RequiresExactPython()
    {
        Assert.IsTrue(PipWheelService.IsMatchingSageWheel(V211Cu128Torch270Cp310, "2.7.0", "cu128", Py310));
        Assert.IsFalse(PipWheelService.IsMatchingSageWheel(V211Cu128Torch270Cp310, "2.7.0", "cu128", Py312));
    }

    [TestMethod]
    public void CudaIndexMismatch_Rejects()
    {
        Assert.IsFalse(PipWheelService.IsMatchingSageWheel(Post6Cu128Torch210, "2.10.0", "cu130", Py312));
        Assert.IsFalse(PipWheelService.IsMatchingSageWheel(Post6Cu128Torch210, "2.10.0", "", Py312));
    }

    [TestMethod]
    public void FallbackWheel_Torch210Cu130_UsesPost6AndHigher()
    {
        var url = PipWheelService.FindFallbackWheelUrl(TorchInfo("2.10.0+cu130"), Py312, null);
        StringAssert.Contains(url, "cu130torch2.10.0andhigher.post6");
    }

    [TestMethod]
    public void FallbackWheel_Torch29Cu128_UsesPost6Pinned()
    {
        var url = PipWheelService.FindFallbackWheelUrl(TorchInfo("2.9.0+cu128"), Py310, null);
        StringAssert.Contains(url, "cu128torch2.9.1.post6");
    }

    [TestMethod]
    public void FallbackWheel_LegacyTorch_UsesPost3()
    {
        var url = PipWheelService.FindFallbackWheelUrl(TorchInfo("2.6.0+cu126"), Py310, null);
        StringAssert.Contains(url, "cu126torch2.6.0.post3");
    }

    [TestMethod]
    public void FallbackWheel_UnknownCombination_ReturnsNull()
    {
        Assert.IsNull(PipWheelService.FindFallbackWheelUrl(TorchInfo("2.10.0+cu126"), Py312, null));
        Assert.IsNull(PipWheelService.FindFallbackWheelUrl(TorchInfo("2.4.0+cu121"), Py310, null));
    }

    [TestMethod]
    public void FallbackWheel_HonorsPinnedSageVersion()
    {
        Assert.IsNotNull(PipWheelService.FindFallbackWheelUrl(TorchInfo("2.10.0+cu130"), Py312, "2.2.0"));
        Assert.IsNull(PipWheelService.FindFallbackWheelUrl(TorchInfo("2.10.0+cu130"), Py312, "9.9.9"));
    }

    [TestMethod]
    [DataRow("2.9.0", true)]
    [DataRow("2.10.0", true)]
    [DataRow("2.11.0", true)]
    [DataRow("2.8.1", false)]
    [DataRow("invalid", false)]
    public void Torch29AndHigherWheel_UsesSharedCompatibilityRules(string torch, bool expected)
    {
        const string version = "2.2.0+cu130torch2.9.0andhigher.post4";
        Assert.AreEqual(expected, PipWheelService.IsCompatibleSageAttention(version, $"{torch}+cu130"));
        Assert.AreEqual(
            expected,
            PipWheelService.IsMatchingSageWheel(
                $"sageattention-{version}-cp39-abi3-win_amd64.whl",
                torch,
                "cu130",
                Py312
            )
        );
    }

    [TestMethod]
    [DataRow("2.2.0+cu128torch2.9.1.post6", "2.9.1+cu1280")]
    [DataRow("2.2.0+cu128torch2.9.1.post6", "2.9.1")]
    [DataRow("2.2.0+cu128torch2.9.1.post6", "2.9.1+cpu")]
    [DataRow("2.2.0+cu128torch2.9.1.post6", "2.9.1+rocm7.2")]
    [DataRow("2.2.0+cu128torch2.9.1.post6", "2.9.1.dev20260101+cu128")]
    public void UnsupportedTorchMetadata_IsNotDeclaredCompatible(string sage, string torch)
    {
        Assert.IsFalse(PipWheelService.IsCompatibleSageAttention(sage, torch));
    }

    [TestMethod]
    public async Task WindowsRepair_EmptyReleaseListUsesPost6WithoutTorchDependencies()
    {
        if (!Compat.IsWindows)
            Assert.Inconclusive("Windows wheel installation");
        var github = Substitute.For<IGithubApiCache>();
        github.GetAllReleases("woct0rdho", "SageAttention").Returns([]);
        var venv = Substitute.For<IPyVenvRunner>();
        venv.Version.Returns(Py312);
        venv.PipShow("torch").Returns(TorchInfo("2.10.0+cu130"));
        var service = new PipWheelService(
            github,
            Substitute.For<IDownloadService>(),
            Substitute.For<IPrerequisiteHelper>(),
            Substitute.For<ISettingsManager>()
        );

        await service.InstallSageAttentionAsync(venv, new GpuInfo { Name = "NVIDIA RTX 4090" });

        await venv.Received(1)
            .PipInstall(
                Arg.Is<ProcessArgs>(args =>
                    args.ToString().Contains("--no-deps")
                    && args.ToString().Contains("cu130torch2.10.0andhigher.post6")
                ),
                Arg.Any<Action<ProcessOutput>?>()
            );
    }

    [TestMethod]
    public async Task NonNvidiaGpu_DoesNotInspectOrInstallCudaWheels()
    {
        var github = Substitute.For<IGithubApiCache>();
        var venv = Substitute.For<IPyVenvRunner>();
        var service = new PipWheelService(
            github,
            Substitute.For<IDownloadService>(),
            Substitute.For<IPrerequisiteHelper>(),
            Substitute.For<ISettingsManager>()
        );
        await service.InstallSageAttentionAsync(venv, new GpuInfo { Name = "AMD Radeon RX 9070" });
        Assert.AreEqual(0, venv.ReceivedCalls().Count());
        Assert.AreEqual(0, github.ReceivedCalls().Count());
    }

    [TestMethod]
    public async Task WindowsMissingTorch_DoesNotLookUpOrInstallWheels()
    {
        if (!Compat.IsWindows)
            Assert.Inconclusive("Windows wheel installation");
        var github = Substitute.For<IGithubApiCache>();
        var venv = Substitute.For<IPyVenvRunner>();
        var service = new PipWheelService(
            github,
            Substitute.For<IDownloadService>(),
            Substitute.For<IPrerequisiteHelper>(),
            Substitute.For<ISettingsManager>()
        );
        await service.InstallSageAttentionAsync(venv, new GpuInfo { Name = "NVIDIA RTX 4090" });
        await venv.DidNotReceive().PipInstall(Arg.Any<ProcessArgs>(), Arg.Any<Action<ProcessOutput>?>());
        Assert.AreEqual(0, github.ReceivedCalls().Count());
    }

    private static PipShowResult TorchInfo(string version) => new() { Name = "torch", Version = version };
}
