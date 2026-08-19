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

    private static PipShowResult TorchInfo(string version) => new() { Name = "torch", Version = version };
}
