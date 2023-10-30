using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Database;

namespace StabilityMatrix.Tests.Models;

[TestClass]
public class LocalImageFileTests
{
    [TestMethod]
    public void TestComparer()
    {
        var comparer = LocalImageFile.Comparer;

        var file1 = new LocalImageFile
        {
            AbsolutePath = "same",
            GenerationParameters = new GenerationParameters { Width = 10 }
        };

        var file2 = new LocalImageFile
        {
            AbsolutePath = "same",
            GenerationParameters = new GenerationParameters { Width = 10 }
        };

        var file3 = new LocalImageFile { AbsolutePath = "different" };

        Assert.IsTrue(comparer.Equals(file1, file2));
        Assert.AreEqual(comparer.GetHashCode(file1), comparer.GetHashCode(file2));
        Assert.IsFalse(comparer.Equals(file1, file3));
        Assert.AreNotEqual(comparer.GetHashCode(file1), comparer.GetHashCode(file3));
    }
}
