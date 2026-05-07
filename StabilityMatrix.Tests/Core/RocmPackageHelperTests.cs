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
    public void WindowsRocmSupport_TryGetMultiArchDeviceExtra_ReturnsExpectedExtra_ForSupportedArch()
    {
        var deviceExtra = WindowsRocmSupport.TryGetMultiArchDeviceExtra("gfx1201");

        Assert.AreEqual("device-gfx1201", deviceExtra);
    }

    [TestMethod]
    public void WindowsRocmSupport_TryGetMultiArchDeviceExtra_ReturnsExpectedExtra_ForCanonicalVega20Arch()
    {
        var deviceExtra = WindowsRocmSupport.TryGetMultiArchDeviceExtra("gfx906");

        Assert.AreEqual("device-gfx906", deviceExtra);
    }

    [TestMethod]
    public void WindowsRocmSupport_TryGetCanonicalArchitecture_ReturnsCanonicalArch_WhenAlreadyCanonical()
    {
        var canonicalArch = WindowsRocmSupport.TryGetCanonicalArchitecture("gfx906");

        Assert.AreEqual("gfx906", canonicalArch);
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
    public void WindowsRocmSupport_TryGetMultiArchDeviceExtra_ReturnsExpectedExtra_ForKrakenPoint()
    {
        var deviceExtra = WindowsRocmSupport.TryGetMultiArchDeviceExtra("gfx1152");

        Assert.AreEqual("device-gfx1152", deviceExtra);
    }

    [TestMethod]
    public void WindowsRocmSupport_IsSupportedGpu_ReturnsTrue_ForSupportedAmdGpu()
    {
        var gpu = new GpuInfo { Name = "AMD Radeon RX 9070 XT", MemoryBytes = 16UL * Size.GiB };

        Assert.IsTrue(WindowsRocmSupport.IsSupportedGpu(gpu));
    }
}
