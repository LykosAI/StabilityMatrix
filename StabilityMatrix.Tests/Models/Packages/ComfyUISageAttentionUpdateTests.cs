using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Helper.HardwareInfo;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Models.Rocm;
using StabilityMatrix.Core.Models.Settings;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;
using StabilityMatrix.Core.Services.Rocm;

namespace StabilityMatrix.Tests.Models.Packages;

[TestClass]
public class ComfyUISageAttentionUpdateTests
{
    private readonly IPyVenvRunner venv = Substitute.For<IPyVenvRunner>();
    private readonly IPipWheelService wheels = Substitute.For<IPipWheelService>();
    private readonly IRocmPackageHelper rocm = Substitute.For<IRocmPackageHelper>();
    private readonly ISettingsManager settings = Substitute.For<ISettingsManager>();
    private ComfyUI comfy = null!;

    [TestInitialize]
    public void Setup()
    {
        settings.Settings.Returns(new Settings { PreferredGpu = new GpuInfo { Name = "NVIDIA RTX 4090" } });
        rocm.GetCompatibility().Returns(new RocmCompatibilityResult { IsCompatible = true });
        comfy = new ComfyUI(
            Substitute.For<IGithubApiCache>(),
            settings,
            Substitute.For<IDownloadService>(),
            Substitute.For<IPrerequisiteHelper>(),
            Substitute.For<IPyInstallationManager>(),
            wheels,
            rocm
        );
    }

    [TestMethod]
    [DataRow("2.2.0+cu128torch2.9.1.post6", "2.9.0+cu128")]
    [DataRow("2.2.0+cu130torch2.9.1.post6", "2.9.2+cu130")]
    [DataRow("2.2.0+cu128torch2.10.0andhigher.post6", "2.11.0+cu128")]
    [DataRow("2.2.0+cu130torch2.9.0andhigher.post4", "2.10.0+cu130")]
    [DataRow("2.2.0+cu128torch2.8.0.post3", "2.8.1+cu128")]
    public async Task CompatibleInstalledWheel_IsPreserved(string sage, string torch)
    {
        SetVersions(sage, torch);
        await comfy.UpdateSageAttentionAsync(venv, TorchIndex.Cuda);
        Assert.AreEqual(0, wheels.ReceivedCalls().Count());
        await venv.Received(1).PipShow("sageattention");
        await venv.Received(1).PipShow("torch");
    }

    [TestMethod]
    [DataRow("2.2.0+cu128torch2.9.1.post6", "2.10.0+cu128")]
    [DataRow("2.2.0+cu128torch2.10.0andhigher.post6", "2.9.1+cu128")]
    [DataRow("2.2.0+cu128torch2.9.1.post6", "2.9.1+cu130")]
    [DataRow("2.2.0", "2.10.0+cu130")]
    [DataRow("malformed", "malformed")]
    public async Task IncompatibleInstalledWheel_UsesDynamicInstaller(string sage, string torch)
    {
        SetVersions(sage, torch);
        await comfy.UpdateSageAttentionAsync(venv, TorchIndex.Cuda);
        await wheels.Received(1).InstallSageAttentionAsync(venv, settings.Settings.PreferredGpu, null);
    }

    [TestMethod]
    [DataRow(null, "2.10.0+cu130")]
    [DataRow("2.2.0+cu130torch2.10.0andhigher.post6", null)]
    public async Task MissingPackage_DoesNotInstallSage(string? sage, string? torch)
    {
        SetVersions(sage, torch);
        await comfy.UpdateSageAttentionAsync(venv, TorchIndex.Cuda);
        Assert.AreEqual(0, wheels.ReceivedCalls().Count());
    }

    [TestMethod]
    public async Task WindowsRocm_RemainsOnItsOwnInstallerPath()
    {
        if (!StabilityMatrix.Core.Helper.Compat.IsWindows)
            Assert.Inconclusive("Windows native ROCm guard");
        await comfy.UpdateSageAttentionAsync(venv, TorchIndex.Rocm);
        Assert.AreEqual(0, venv.ReceivedCalls().Count());
        Assert.AreEqual(0, wheels.ReceivedCalls().Count());
    }

    [TestMethod]
    public async Task InspectionFailure_DoesNotAbortComfyUpdate()
    {
        venv.PipShow("sageattention").ThrowsAsync(new InvalidOperationException("pip show failed"));
        await comfy.UpdateSageAttentionAsync(venv, TorchIndex.Cuda);
        Assert.AreEqual(0, wheels.ReceivedCalls().Count());
    }

    [TestMethod]
    public async Task RepairFailure_DoesNotAbortComfyUpdate()
    {
        SetVersions("2.2.0+cu128torch2.9.1.post6", "2.10.0+cu130");
        wheels
            .InstallSageAttentionAsync(venv, settings.Settings.PreferredGpu, null)
            .ThrowsAsync(new InvalidOperationException("installation failed"));
        await comfy.UpdateSageAttentionAsync(venv, TorchIndex.Cuda);
        await wheels.Received(1).InstallSageAttentionAsync(venv, settings.Settings.PreferredGpu, null);
    }

    private void SetVersions(string? sage, string? torch)
    {
        venv.PipShow("sageattention")
            .Returns(sage is null ? null : new PipShowResult { Name = "sageattention", Version = sage });
        venv.PipShow("torch")
            .Returns(torch is null ? null : new PipShowResult { Name = "torch", Version = torch });
    }
}
