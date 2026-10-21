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
    public void RocmSupport_TryGetMultiArchDeviceExtra_ReturnsExpectedExtra_ForSupportedArch()
    {
        var deviceExtra = RocmSupport.TryGetMultiArchDeviceExtra("gfx1201");

        Assert.AreEqual("device-gfx1201", deviceExtra);
    }

    [TestMethod]
    public void RocmSupport_TryGetMultiArchDeviceExtra_ReturnsExpectedExtra_ForCanonicalVega20Arch()
    {
        var deviceExtra = RocmSupport.TryGetMultiArchDeviceExtra("gfx906");

        Assert.AreEqual("device-gfx906", deviceExtra);
    }

    [TestMethod]
    public void RocmSupport_TryGetCanonicalArchitecture_ReturnsCanonicalArch_WhenAlreadyCanonical()
    {
        var canonicalArch = RocmSupport.TryGetCanonicalArchitecture("gfx906");

        Assert.AreEqual("gfx906", canonicalArch);
    }

    [TestMethod]
    public void IsUsableRocmTorchBuild_ReturnsTrue_WhenHipMetadataExists()
    {
        var isUsable = RocmPackageHelper.IsUsableRocmTorchBuild(
            version: "test-version",
            hipVersion: "test-hip-version"
        );

        Assert.IsTrue(isUsable);
    }

    [TestMethod]
    public void IsUsableRocmTorchBuild_ReturnsTrue_WhenVersionContainsRocm()
    {
        var isUsable = RocmPackageHelper.IsUsableRocmTorchBuild(
            version: "test-version+rocm",
            hipVersion: null
        );

        Assert.IsTrue(isUsable);
    }

    [TestMethod]
    public void IsUsableRocmTorchBuild_ReturnsFalse_WhenNoRocmMetadataExists()
    {
        var isUsable = RocmPackageHelper.IsUsableRocmTorchBuild(version: "test-version", hipVersion: null);

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
    public void RocmSupport_TryGetMultiArchDeviceExtra_ReturnsExpectedExtra_ForKrakenPoint()
    {
        var deviceExtra = RocmSupport.TryGetMultiArchDeviceExtra("gfx1152");

        Assert.AreEqual("device-gfx1152", deviceExtra);
    }

    [TestMethod]
    public void RocmSupport_IsSupportedGpu_ReturnsTrue_ForSupportedAmdGpu()
    {
        var gpu = new GpuInfo { Name = "AMD Radeon RX 9070 XT", MemoryBytes = 16UL * Size.GiB };

        Assert.IsTrue(RocmSupport.IsSupportedGpu(gpu));
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
    [DataRow("gfx908")]
    [DataRow("gfx90a")]
    [DataRow("gfx942")]
    [DataRow("gfx950")]
    public void RocmSupport_IsSupportedArchitecture_AcceptsCdnaTargets(string arch)
    {
        Assert.IsTrue(RocmSupport.IsSupportedArchitecture(arch));
    }

    [TestMethod]
    [DataRow("AMD Instinct MI100", "gfx908")]
    [DataRow("AMD Instinct MI210", "gfx90a")]
    public void GpuInfo_GetAmdGfxArch_ResolvesInstinctCdnaTargets(string name, string expected)
    {
        var gpu = new GpuInfo { Name = name };

        Assert.AreEqual(expected, gpu.GetAmdGfxArch());
    }

    [TestMethod]
    [DataRow("gfx908", true)]
    [DataRow("gfx90a", false)]
    [DataRow("gfx942", false)]
    [DataRow("gfx950", false)]
    [DataRow("gfx1030", true)]
    [DataRow("gfx1100", false)]
    [DataRow("gfx1201", false)]
    public void RocmSupport_PreferMathSdpaFallback_MatchesAotritonImageList(string arch, bool expectFallback)
    {
        // AOTriton ships images for gfx90a, gfx942, gfx950, gfx11xx and gfx120x. Everything else has no
        // fused SDPA kernels and must be forced onto the math backend.
        Assert.AreEqual(expectFallback, RocmSupport.PreferMathSdpaFallback(arch));
    }

    [TestMethod]
    [DataRow(110000, "gfx1100")]
    [DataRow(120001, "gfx1201")]
    [DataRow(100300, "gfx1030")]
    [DataRow(90402, "gfx942")]
    [DataRow(90010, "gfx90a")]
    [DataRow(110500, "gfx1150")]
    public void RocmArchDetector_FormatGfxTarget_DecodesKfdTargetVersion(int version, string expected)
    {
        Assert.AreEqual(expected, RocmArchDetector.FormatGfxTarget(version));
    }

    [TestMethod]
    public void RocmArchDetector_TryParseNode_SkipsCpuNodes()
    {
        const string cpuNode = "cpu_cores_count 16\nsimd_count 0\ngfx_target_version 0\n";

        Assert.IsNull(RocmArchDetector.TryParseNode(cpuNode, 0));
    }

    [TestMethod]
    public void RocmArchDetector_TryParseNode_ParsesGpuNodes()
    {
        const string gpuNode = "simd_count 192\ngfx_target_version 110000\ndevice_id 29772\n";

        var device = RocmArchDetector.TryParseNode(gpuNode, 1);

        Assert.IsNotNull(device);
        Assert.AreEqual("gfx1100", device.GfxArch);
        Assert.AreEqual(1, device.DeviceIndex);
        Assert.AreEqual(192, device.SimdCount);
    }

    [TestMethod]
    public void RocmArchDetector_SelectPreferred_PrefersTheDiscreteDevice()
    {
        // An APU is a couple of SIMD units; the discrete card is two orders of magnitude more. Node order
        // carries no information about which is which, so both orders have to resolve the same way.
        var apuFirst = new[]
        {
            new RocmDetectedDevice
            {
                GfxArch = "gfx1036",
                DeviceIndex = 0,
                SimdCount = 4,
            },
            new RocmDetectedDevice
            {
                GfxArch = "gfx1100",
                DeviceIndex = 1,
                SimdCount = 192,
            },
        };
        var dgpuFirst = new[]
        {
            new RocmDetectedDevice
            {
                GfxArch = "gfx1100",
                DeviceIndex = 0,
                SimdCount = 192,
            },
            new RocmDetectedDevice
            {
                GfxArch = "gfx1036",
                DeviceIndex = 1,
                SimdCount = 4,
            },
        };

        Assert.AreEqual("gfx1100", RocmArchDetector.SelectPreferred(apuFirst)!.GfxArch);
        Assert.AreEqual("gfx1100", RocmArchDetector.SelectPreferred(dgpuFirst)!.GfxArch);
    }

    [TestMethod]
    public void RocmArchDetector_SelectPreferred_BreaksTiesOnDeviceIndex()
    {
        var first = new RocmDetectedDevice
        {
            GfxArch = "gfx1100",
            DeviceIndex = 0,
            SimdCount = 192,
        };
        var second = new RocmDetectedDevice
        {
            GfxArch = "gfx1100",
            DeviceIndex = 1,
            SimdCount = 192,
        };

        Assert.AreEqual(0, RocmArchDetector.SelectPreferred([first, second])!.DeviceIndex);
    }

    [TestMethod]
    public void SelectPreferredGpu_PrefersTheCardWithMoreMemory()
    {
        // On Windows the APU reports its carveout, so memory is the only discrete signal available.
        var apu = new GpuInfo { Name = "AMD Radeon(TM) Graphics", MemoryBytes = 512UL * 1024 * 1024 };
        var dgpu = new GpuInfo { Name = "AMD Radeon RX 7900 XTX", MemoryBytes = 24UL * 1024 * 1024 * 1024 };

        Assert.AreEqual(dgpu, RocmPackageHelper.SelectPreferredGpu([apu, dgpu]));
        Assert.AreEqual(dgpu, RocmPackageHelper.SelectPreferredGpu([dgpu, apu]));
    }

    [TestMethod]
    public void ResolveTarget_WindowsStable_KeepsAmdFeedAndDeviceExtra()
    {
        var target = RocmSupport.ResolveTarget("gfx1201");

        Assert.AreEqual(RocmIndexKind.AmdStable, target.IndexKind);
        Assert.AreEqual(RocmSupport.StableMultiArchPythonPackageIndexUrl, target.IndexUrl);
        Assert.AreEqual("device-gfx1201", target.DeviceExtra);
        Assert.IsTrue(target.SupportsDeviceExtras);
        Assert.IsTrue(target.SupportsSdkDevel);
        Assert.IsTrue(target.RequiresPairResolution);
    }

    [TestMethod]
    public void ResolveTarget_VegaUsesNightlyFeed()
    {
        var target = RocmSupport.ResolveTarget("gfx906");

        Assert.AreEqual(RocmIndexKind.AmdNightly, target.IndexKind);
        Assert.AreEqual(RocmSupport.NightlyMultiArchPythonPackageIndexUrl, target.IndexUrl);
    }

    [TestMethod]
    public void ResolveTarget_LinuxAmdFeed_KeepsDeviceExtras()
    {
        // The feed publishes linux_x86_64 device packages, so the extra applies on both platforms.
        var target = RocmSupport.ResolveTarget("gfx1100");

        Assert.AreEqual(RocmSupport.StableMultiArchPythonPackageIndexUrl, target.IndexUrl);
        Assert.AreEqual("device-gfx1100", target.DeviceExtra);
    }

    [TestMethod]
    public void ResolveTarget_PyTorchOverride_DropsDeviceExtrasAndSdkDevel()
    {
        var target = RocmSupport.ResolveTarget("gfx1100", RocmIndexKind.PyTorchRocm72);

        Assert.AreEqual("https://download.pytorch.org/whl/rocm7.2", target.IndexUrl);
        Assert.IsNull(target.DeviceExtra);
        Assert.IsFalse(target.SupportsDeviceExtras);
        Assert.IsFalse(target.SupportsSdkDevel);
        Assert.IsFalse(target.RequiresPairResolution);
    }

    [TestMethod]
    public void ResolveTarget_UnsupportedArch_HasNoDeviceExtra()
    {
        var target = RocmSupport.ResolveTarget("gfx9999");

        Assert.IsNull(target.DeviceExtra);
        Assert.AreEqual(RocmIndexKind.AmdStable, target.IndexKind);
    }

    [TestMethod]
    public void ApplyDeviceVisibility_PinsSelectedDeviceWhenSeveralAmdGpusExist()
    {
        var environment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        RocmPackageHelper.ApplyDeviceVisibility(
            environment,
            new RocmRuntimeContext
            {
                IsSupported = true,
                AmdDeviceCount = 2,
                DeviceIndex = 1,
            }
        );

        Assert.AreEqual("1", environment["ROCR_VISIBLE_DEVICES"]);
    }

    [TestMethod]
    public void ApplyDeviceVisibility_LeavesSingleGpuMachinesAlone()
    {
        var environment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        RocmPackageHelper.ApplyDeviceVisibility(
            environment,
            new RocmRuntimeContext
            {
                IsSupported = true,
                AmdDeviceCount = 1,
                DeviceIndex = 0,
            }
        );

        Assert.IsFalse(environment.ContainsKey("ROCR_VISIBLE_DEVICES"));
    }

    [TestMethod]
    public void ApplyDeviceVisibility_SkipsWhenThePlatformReportsNoIndex()
    {
        var environment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        RocmPackageHelper.ApplyDeviceVisibility(
            environment,
            new RocmRuntimeContext
            {
                IsSupported = true,
                AmdDeviceCount = 2,
                DeviceIndex = null,
            }
        );

        Assert.IsFalse(environment.ContainsKey("ROCR_VISIBLE_DEVICES"));
    }

    [TestMethod]
    public void ApplyRocmSdkEnvironmentSanitization_MarksInheritedSdkVariablesForRemoval()
    {
        var environment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ROCM_PATH"] = "/opt/rocm",
            ["PATH"] = "/usr/bin",
        };

        RocmPackageHelper.ApplyRocmSdkEnvironmentSanitization(environment);

        foreach (var key in RocmSupport.ConflictingRocmEnvironmentVariables)
        {
            Assert.AreEqual(ProcessRunner.UnsetEnvironmentVariable, environment[key], key);
        }

        Assert.AreEqual("/usr/bin", environment["PATH"]);
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
