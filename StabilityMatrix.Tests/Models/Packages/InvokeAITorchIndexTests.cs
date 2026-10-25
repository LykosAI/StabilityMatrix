using System.Linq;
using NSubstitute;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Helper.HardwareInfo;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Models.Settings;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;
using StabilityMatrix.Core.Services.Rocm;

namespace StabilityMatrix.Tests.Models.Packages;

[TestClass]
public class InvokeAITorchIndexTests
{
    [TestMethod]
    public void OffersIpexTorchIndex()
    {
        var invoke = new InvokeAI(
            Substitute.For<IGithubApiCache>(),
            Substitute.For<ISettingsManager>(),
            Substitute.For<IDownloadService>(),
            Substitute.For<IPrerequisiteHelper>(),
            Substitute.For<IPyInstallationManager>(),
            Substitute.For<IPipWheelService>(),
            Substitute.For<IRocmPackageHelper>()
        );

        CollectionAssert.Contains(invoke.AvailableTorchIndices.ToList(), TorchIndex.Ipex);
    }

    [TestMethod]
    public void RecommendsIpexForIntelArc()
    {
        if (Compat.IsMacOS)
        {
            Assert.Inconclusive("Intel Arc is not a macOS backend.");
        }

        var settings = Substitute.For<ISettingsManager>();
        settings.Settings.Returns(new Settings { PreferredGpu = new GpuInfo { Name = "Intel Arc A730M" } });

        var invoke = new InvokeAI(
            Substitute.For<IGithubApiCache>(),
            settings,
            Substitute.For<IDownloadService>(),
            Substitute.For<IPrerequisiteHelper>(),
            Substitute.For<IPyInstallationManager>(),
            Substitute.For<IPipWheelService>(),
            Substitute.For<IRocmPackageHelper>()
        );

        Assert.AreEqual(TorchIndex.Ipex, invoke.GetRecommendedTorchVersion());
    }
}
