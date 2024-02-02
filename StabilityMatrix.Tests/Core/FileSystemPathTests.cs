using StabilityMatrix.Core.Models.FileInterfaces;

namespace StabilityMatrix.Tests.Core;

[TestClass]
public class FileSystemPathTests
{
    [DataTestMethod]
    [DataRow("M:\\Path", "M:\\Path")]
    [DataRow("root\\abc", "root\\abc")]
    [DataRow("root/abc", "root/abc")]
    public void TestFilePathEquals(string left, string right)
    {
        // Arrange
        var leftPath = new FilePath(left);
        var rightPath = new FilePath(right);

        // Act
        var resultEquals = leftPath.Equals(rightPath);
        var resultOperator = leftPath == rightPath;
        var resultNotOperator = leftPath != rightPath;

        // Assert
        Assert.IsTrue(resultEquals);
        Assert.IsTrue(resultOperator);
        Assert.IsFalse(resultNotOperator);
    }

    [DataTestMethod]
    [DataRow("M:\\Path", "M:\\Path2")]
    [DataRow("root\\abc", "root\\abc2")]
    public void TestFilePathNotEquals(string left, string right)
    {
        // Arrange
        var leftPath = new FilePath(left);
        var rightPath = new FilePath(right);

        // Act
        var resultEquals = leftPath.Equals(rightPath);
        var resultOperator = leftPath == rightPath;
        var resultNotOperator = leftPath != rightPath;

        // Assert
        Assert.IsFalse(resultEquals);
        Assert.IsFalse(resultOperator);
        Assert.IsTrue(resultNotOperator);
    }

    [DataTestMethod]
    [DataRow("M:\\Path", "M:\\Path")]
    [DataRow("M:\\Path", "M:\\Path\\")]
    [DataRow("root/abc", "root/abc")]
    [DataRow("root/abc", "root/abc/")]
    public void TestDirectoryPathEquals(string left, string right)
    {
        // Arrange
        var leftPath = new DirectoryPath(left);
        var rightPath = new DirectoryPath(right);

        // Act
        var resultEquals = leftPath.Equals(rightPath);
        var resultOperator = leftPath == rightPath;
        var resultNotOperator = leftPath != rightPath;

        // Assert
        Assert.IsTrue(resultEquals);
        Assert.IsTrue(resultOperator);
        Assert.IsFalse(resultNotOperator);
    }
}
