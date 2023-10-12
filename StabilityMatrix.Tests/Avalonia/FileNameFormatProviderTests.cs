using System.ComponentModel.DataAnnotations;
using StabilityMatrix.Avalonia.Models.Inference;

namespace StabilityMatrix.Tests.Avalonia;

[TestClass]
public class FileNameFormatProviderTests
{
    [TestMethod]
    public void TestFileNameFormatProviderValidate_Valid_ShouldNotThrow()
    {
        var provider = new FileNameFormatProvider();

        var result = provider.Validate("{date}_{time}-{model_name}-{seed}");
        Assert.AreEqual(ValidationResult.Success, result);
    }

    [TestMethod]
    public void TestFileNameFormatProviderValidate_Invalid_ShouldThrow()
    {
        var provider = new FileNameFormatProvider();

        var result = provider.Validate("{date}_{time}-{model_name}-{seed}-{invalid}");
        Assert.AreNotEqual(ValidationResult.Success, result);

        Assert.AreEqual("Unknown variable 'invalid'", result.ErrorMessage);
    }
}
