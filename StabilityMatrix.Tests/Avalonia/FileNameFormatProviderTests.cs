using StabilityMatrix.Avalonia.Models.Inference;

namespace StabilityMatrix.Tests.Avalonia;

[TestClass]
public class FileNameFormatProviderTests
{
    [TestMethod]
    public void TestFileNameFormatProviderValidate_Valid_ShouldNotThrow()
    {
        var provider = new FileNameFormatProvider();

        provider.Validate("{date}_{time}-{model_name}-{seed}");
    }

    [TestMethod]
    public void TestFileNameFormatProviderValidate_Invalid_ShouldThrow()
    {
        var provider = new FileNameFormatProvider();

        Assert.ThrowsException<ArgumentException>(
            () => provider.Validate("{date}_{time}-{model_name}-{seed}-{invalid}")
        );
    }
}
