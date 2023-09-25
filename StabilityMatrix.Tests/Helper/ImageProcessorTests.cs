using StabilityMatrix.Avalonia.Helpers;

namespace StabilityMatrix.Tests.Helper;

[TestClass]
public class ImageProcessorTests
{
    [DataTestMethod]
    [DataRow(0, 1, 1)]
    [DataRow(1, 1, 1)]
    [DataRow(4, 2, 2)]
    [DataRow(8, 2, 4)]
    [DataRow(12, 3, 4)]
    [DataRow(20, 4, 5)]
    public void TestGetGridDimensionsFromImageCount(int count, int expectedRow, int expectedCols)
    {
        var result = ImageProcessor.GetGridDimensionsFromImageCount(count);
        Assert.AreEqual(expectedRow, result.rows);
        Assert.AreEqual(expectedCols, result.columns);
    }
}
