using System.Text.Json;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.HardwareInfo;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Models.Rocm;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
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

    [TestMethod]
    public void BuildRocmTorchArgs_PinsTorchAndTorchvisionWithDeviceExtras()
    {
        var args = RocmPackageHelper.BuildRocmTorchArgs(
            "https://stable.repo.amd.com/rocm/whl-next/",
            "torch[device-gfx1201]==2.14.0",
            "torchvision[device-gfx1201]==0.29.0a0"
        );

        var text = args.ToString();

        StringAssert.Contains(text, "torch[device-gfx1201]==2.14.0");
        StringAssert.Contains(text, "torchvision[device-gfx1201]==0.29.0a0");
        StringAssert.Contains(text, "torchaudio");
    }

    [TestMethod]
    public void ApplyRocmTorchInstallOptions_ScopesUpgradeAndReinstallToTorchTrio()
    {
        var args = RocmPackageHelper.ApplyRocmTorchInstallOptions(
            new PipInstallArgs().AddArg("torch[device-gfx1201]==2.14.0"),
            new PipInstallConfig { UpgradePackages = true, ForceReinstallTorch = true },
            pipOverrides: null
        );

        var values = args.Arguments.Select(argument => argument.Value).ToList();
        var expectedTargets = new[] { "torch", "torchvision", "torchaudio" };

        // A blanket --upgrade would re-resolve numpy (and friends) against the multi-arch index; a blanket
        // --force-reinstall would reinstall the whole graph. Neither may come back.
        CollectionAssert.DoesNotContain(values, "--upgrade");
        CollectionAssert.DoesNotContain(values, "--force-reinstall");

        Assert.AreEqual(3, values.Count(value => value == "--upgrade-package"));
        Assert.AreEqual(3, values.Count(value => value == "--reinstall-package"));
        CollectionAssert.AreEqual(expectedTargets, TargetsOf(values, "--upgrade-package"));
        CollectionAssert.AreEqual(expectedTargets, TargetsOf(values, "--reinstall-package"));
    }

    [TestMethod]
    public void ApplyRocmTorchInstallOptions_WithoutUpgradePackages_ReinstallsTrioWithoutUpgrading()
    {
        var args = RocmPackageHelper.ApplyRocmTorchInstallOptions(
            new PipInstallArgs().AddArg("torch[device-gfx1201]==2.14.0"),
            new PipInstallConfig { UpgradePackages = false, ForceReinstallTorch = true },
            pipOverrides: null
        );

        var values = args.Arguments.Select(argument => argument.Value).ToList();

        CollectionAssert.DoesNotContain(values, "--upgrade");
        CollectionAssert.DoesNotContain(values, "--upgrade-package");
        Assert.AreEqual(3, values.Count(value => value == "--reinstall-package"));
    }

    private static string[] TargetsOf(List<string> values, string flag) =>
        values
            .Select((value, index) => (value, index))
            .Where(pair => pair.value == flag)
            .Select(pair => values[pair.index + 1])
            .ToArray();

    [TestMethod]
    public void BuildRocmTorchArgs_UserTorchOverrideReplacesPinnedTorch()
    {
        var args = RocmPackageHelper.BuildRocmTorchArgs(
            "https://stable.repo.amd.com/rocm/whl-next/",
            "torch[device-gfx1201]==2.14.0",
            "torchvision[device-gfx1201]==0.29.0a0"
        );

        var overrides = new List<PipPackageSpecifierOverride>
        {
            new()
            {
                Name = "torch",
                Constraint = "==",
                Version = "2.13.0",
                Action = PipPackageSpecifierOverrideAction.Update,
            },
        };

        var text = args.WithUserOverrides(overrides).ToString();

        // The override must replace the pin rather than be appended next to it (pip cannot satisfy both).
        StringAssert.Contains(text, "torch==2.13.0");
        Assert.IsFalse(text.Contains("torch[device-gfx1201]==2.14.0"), text);
    }

    [TestMethod]
    public void BuildRocmTorchArgs_UserTorchvisionOverrideReplacesPinnedTorchvision()
    {
        var args = RocmPackageHelper.BuildRocmTorchArgs(
            "https://stable.repo.amd.com/rocm/whl-next/",
            "torch[device-gfx1201]==2.14.0",
            "torchvision[device-gfx1201]==0.29.0a0"
        );

        var overrides = new List<PipPackageSpecifierOverride>
        {
            new()
            {
                Name = "torchvision",
                Constraint = "==",
                Version = "0.28.0",
                Action = PipPackageSpecifierOverrideAction.Update,
            },
        };

        var text = args.WithUserOverrides(overrides).ToString();

        StringAssert.Contains(text, "torchvision==0.28.0");
        Assert.IsFalse(text.Contains("torchvision[device-gfx1201]==0.29.0a0"), text);
    }
}
