using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;

namespace StabilityMatrix.Tests.Core;

[TestClass]
public class PipInstallArgsTests
{
    [TestMethod]
    public void TestGetTorch()
    {
        // Arrange
        const string version = "==2.1.0";

        // Act
        var args = new PipInstallArgs().WithTorch(version).ToProcessArgs().ToString();

        // Assert
        Assert.AreEqual("torch==2.1.0", args);
    }

    [TestMethod]
    public void TestGetTorchWithExtraIndex()
    {
        // Arrange
        const string version = ">=2.0.0";
        const string index = "cu118";

        // Act
        var args = new PipInstallArgs()
            .WithTorch(version)
            .WithTorchVision()
            .WithTorchExtraIndex(index)
            .ToProcessArgs()
            .ToString();

        // Assert
        Assert.AreEqual(
            "torch>=2.0.0 torchvision --extra-index-url https://download.pytorch.org/whl/cu118",
            args
        );
    }

    [TestMethod]
    public void TestGetTorchWithMoreStuff()
    {
        // Act
        var args = new PipInstallArgs()
            .AddArg("--pre")
            .WithTorch("~=2.0.0")
            .WithTorchVision()
            .WithTorchExtraIndex("nightly/cpu")
            .ToString();

        // Assert
        Assert.AreEqual(
            "--pre torch~=2.0.0 torchvision --extra-index-url https://download.pytorch.org/whl/nightly/cpu",
            args
        );
    }
}
