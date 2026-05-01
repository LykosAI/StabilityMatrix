using System.Text.Json;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.HardwareInfo;
using StabilityMatrix.Core.Models.Rocm;
using StabilityMatrix.Core.Services.Rocm;

namespace StabilityMatrix.Tests.Core;

[TestClass]
public class RocmPackageHelperTests
{
    [TestMethod]
    public void GetRocmSdkDevelAlignmentVersion_ReturnsRocmVersion_WhenVersionsMismatch()
    {
        var targetVersion = RocmPackageHelper.GetRocmSdkDevelAlignmentVersion(
            rocmVersion: "7.13.0a20260416",
            rocmSdkDevelVersion: "7.13.0a20260501"
        );

        Assert.AreEqual("7.13.0a20260416", targetVersion);
    }

    [TestMethod]
    public void GetRocmSdkDevelAlignmentVersion_ReturnsNull_WhenVersionsAlreadyMatch()
    {
        var targetVersion = RocmPackageHelper.GetRocmSdkDevelAlignmentVersion(
            rocmVersion: "7.13.0a20260416",
            rocmSdkDevelVersion: "7.13.0a20260416"
        );

        Assert.IsNull(targetVersion);
    }

    [TestMethod]
    public void GetRocmSdkDevelAlignmentVersion_FallsBackToTorchBuildVersion()
    {
        var targetVersion = RocmPackageHelper.GetRocmSdkDevelAlignmentVersion(
            rocmVersion: null,
            rocmSdkDevelVersion: "7.13.0a20260501",
            torchVersion: "2.11.0+rocm7.13.0a20260416"
        );

        Assert.AreEqual("7.13.0a20260416", targetVersion);
    }

    [TestMethod]
    public void TryExtractRocmBuildVersion_ReturnsNull_WhenTorchVersionHasNoRocmTag()
    {
        var rocmBuildVersion = RocmPackageHelper.TryExtractRocmBuildVersion("2.11.0");

        Assert.IsNull(rocmBuildVersion);
    }

    [TestMethod]
    public void TryExtractRocmBuildVersion_ReturnsVersionSuffix_WhenTorchVersionContainsRocmTag()
    {
        var rocmBuildVersion = RocmPackageHelper.TryExtractRocmBuildVersion("2.11.0+rocm7.13.0a20260416");

        Assert.AreEqual("7.13.0a20260416", rocmBuildVersion);
    }

    [TestMethod]
    public void IsUsableWindowsNativeTorchBuild_ReturnsTrue_WhenHipMetadataExists()
    {
        var isUsable = RocmPackageHelper.IsUsableWindowsNativeTorchBuild(
            version: "test-version",
            hipVersion: "test-hip-version"
        );

        Assert.IsTrue(isUsable);
    }

    [TestMethod]
    public void IsUsableWindowsNativeTorchBuild_ReturnsTrue_WhenVersionContainsRocm()
    {
        var isUsable = RocmPackageHelper.IsUsableWindowsNativeTorchBuild(
            version: "test-version+rocm",
            hipVersion: null
        );

        Assert.IsTrue(isUsable);
    }

    [TestMethod]
    public void IsUsableWindowsNativeTorchBuild_ReturnsFalse_WhenNoRocmMetadataExists()
    {
        var isUsable = RocmPackageHelper.IsUsableWindowsNativeTorchBuild(
            version: "test-version",
            hipVersion: null
        );

        Assert.IsFalse(isUsable);
    }

    [TestMethod]
    public void TryExtractJsonObject_ReturnsJson_WhenOutputContainsDiagnosticPrefix()
    {
        const string output =
            "warning: ROCm topology probe emitted diagnostic output"
            + "\nwarning: continuing with torch verification"
            + "\n{\"version\": \"test-version\", \"hip\": \"test-hip-version\", \"cuda\": false}";

        var json = RocmPackageHelper.TryExtractJsonObject(output);

        Assert.IsNotNull(json);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.AreEqual("test-version", root.GetProperty("version").GetString());
        Assert.AreEqual("test-hip-version", root.GetProperty("hip").GetString());
        Assert.IsFalse(root.GetProperty("cuda").GetBoolean());
    }

    [TestMethod]
    public void TryExtractJsonObject_ReturnsNull_WhenOutputContainsNoJson()
    {
        const string output =
            "warning: ROCm topology probe emitted diagnostic output\n"
            + "warning: no JSON payload was produced";

        var json = RocmPackageHelper.TryExtractJsonObject(output);

        Assert.IsNull(json);
    }

    [TestMethod]
    public void TryGetWindowsNativeRocmRuntimeFailureReason_ReturnsDeviceDetectionMessage()
    {
        const string output = "checkHipErrors() HIP API error = 0100 \"no ROCm-capable device is detected\"";

        var reason = RocmPackageHelper.TryGetWindowsNativeRocmRuntimeFailureReason(output);

        Assert.AreEqual(
            "the installed ROCm runtime could not detect a ROCm-capable GPU on this system.",
            reason
        );
    }

    [TestMethod]
    public void TryGetWindowsNativeRocmRuntimeFailureReason_ReturnsWddmMessage()
    {
        const string output = "warning: No WDDM adapters found.";

        var reason = RocmPackageHelper.TryGetWindowsNativeRocmRuntimeFailureReason(output);

        Assert.AreEqual(
            "the ROCm runtime could not find any compatible WDDM adapters for the current GPU/driver stack.",
            reason
        );
    }

    [TestMethod]
    public void CombineProcessOutput_JoinsStdoutAndStderr()
    {
        var combined = RocmPackageHelper.CombineProcessOutput("stdout line", "stderr line");

        Assert.AreEqual($"stdout line{Environment.NewLine}stderr line", combined);
    }

    [TestMethod]
    public void WindowsRocmSupport_TryGetPackageIndexUrl_ReturnsExpectedIndex_ForKrakenPoint()
    {
        var indexUrl = WindowsRocmSupport.TryGetPackageIndexUrl("gfx1152");

        Assert.AreEqual("https://rocm.nightlies.amd.com/v2-staging/gfx1152/", indexUrl);
    }

    [TestMethod]
    public void WindowsRocmSupport_IsSupportedGpu_ReturnsTrue_ForSupportedAmdGpu()
    {
        var gpu = new GpuInfo { Name = "AMD Radeon RX 9070 XT", MemoryBytes = 16UL * Size.GiB };

        Assert.IsTrue(WindowsRocmSupport.IsSupportedGpu(gpu));
    }
}
