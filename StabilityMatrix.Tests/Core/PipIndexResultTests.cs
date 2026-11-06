using Microsoft.VisualStudio.TestTools.UnitTesting;
using StabilityMatrix.Core.Python;

namespace StabilityMatrix.Tests.Core;

[TestClass]
public class PipIndexResultTests
{
    [TestMethod]
    public void Parse_ReadsAvailableVersions()
    {
        var output = """
            jax (0.4.30)
            Available versions: 0.4.30, 0.4.29, 0.4.28
              INSTALLED: 0.4.26
              LATEST:    0.4.30
            """;

        var result = PipIndexResult.Parse(output);

        CollectionAssert.AreEqual(new[] { "0.4.30", "0.4.29", "0.4.28" }, result.AvailableVersions.ToArray());
    }

    [TestMethod]
    public void Parse_WithoutAvailableVersionsLine_ReturnsNoVersions()
    {
        var output = """
            WARNING: pip index is currently an experimental command. It may be removed/changed in a future release without prior warning.
            """;

        var result = PipIndexResult.Parse(output);

        Assert.AreEqual(0, result.AvailableVersions.Count);
    }
}
